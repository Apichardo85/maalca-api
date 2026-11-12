using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Maalca.Application.Services;

/// <summary>
/// Clover (https://docs.clover.com/dev/reference): token de comerciante de SOLO LECTURA (Inventory, Merchant, Orders, Payments).
/// Se valida contra GET /v3/merchants/{mId} antes de guardarlo cifrado. El resumen de ventas se lee en vivo (no se duplica
/// nada en la base): pagos exitosos de las órdenes del rango. Los montos de Clover vienen en centavos.
/// </summary>
public class CloverService : ICloverService
{
    private const string Provider = "clover";
    private const string BaseUrl = "https://api.clover.com/v3/merchants/";
    private const int PageSize = 500;
    private const int MaxPages = 20; // tope: 10 000 órdenes por consulta
    private static readonly Regex MerchantIdRx = new("^[A-Za-z0-9]{8,20}$", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly string _secret;

    public CloverService(AppDbContext db, IHttpClientFactory http, IConfiguration config)
    {
        _db = db;
        _http = http;
        _secret = config["Jwt:Key"] ?? "MaalcaSecretKey12345678901234567890";
    }

    public async Task<CloverConnectionDto> GetConnectionAsync(Guid affiliateId)
    {
        var c = await _db.IntegrationConnections.AsNoTracking().FirstOrDefaultAsync(x => x.AffiliateId == affiliateId && x.Provider == Provider);
        return ToDto(c);
    }

    public async Task<CloverConnectionDto> ConnectAsync(Guid affiliateId, ConnectCloverRequest r)
    {
        var mid = (r.MerchantId ?? "").Trim().ToUpperInvariant();
        if (!MerchantIdRx.IsMatch(mid)) throw new ArgumentException("Merchant ID inválido. Está en la URL del dashboard de Clover (.../m/XXXXXXXXXXXXX/...).");

        var c = await _db.IntegrationConnections.FirstOrDefaultAsync(x => x.AffiliateId == affiliateId && x.Provider == Provider);
        var token = string.IsNullOrWhiteSpace(r.Token) ? SecretBox.Unprotect(c?.CredentialsEnc, _secret) : r.Token.Trim();
        if (string.IsNullOrEmpty(token)) throw new ArgumentException("Falta el token de API de Clover.");

        // Valida antes de guardar: un token malo o de otro comercio no debe quedar conectado.
        using var doc = await GetAsync(mid, token, "");
        var name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : null;

        if (c is null)
        {
            c = new IntegrationConnection { Id = Guid.NewGuid(), AffiliateId = affiliateId, Provider = Provider, Kind = "pos" };
            _db.IntegrationConnections.Add(c);
        }
        c.CredentialsEnc = SecretBox.Protect(token, _secret);
        c.CredentialsLast4 = token.Length >= 4 ? token[^4..] : token;
        c.ExternalId = mid;
        c.ExternalName = name;
        c.Status = "connected";
        c.LastError = null;
        c.IsActive = true;
        await _db.SaveChangesAsync();
        return ToDto(c);
    }

    public async Task DisconnectAsync(Guid affiliateId)
    {
        var c = await _db.IntegrationConnections.FirstOrDefaultAsync(x => x.AffiliateId == affiliateId && x.Provider == Provider);
        if (c is null) return;
        _db.IntegrationConnections.Remove(c);
        await _db.SaveChangesAsync();
    }

    public async Task<CloverSalesReportDto> GetSalesReportAsync(Guid affiliateId, DateOnly from, DateOnly to)
    {
        if (to < from) throw new ArgumentException("El rango de fechas no es válido.");
        if (to.DayNumber - from.DayNumber > 92) throw new ArgumentException("El rango máximo es de 92 días.");

        var c = await _db.IntegrationConnections.FirstOrDefaultAsync(x => x.AffiliateId == affiliateId && x.Provider == Provider && x.IsActive);
        var token = SecretBox.Unprotect(c?.CredentialsEnc, _secret);
        if (c is null || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(c.ExternalId)) throw new ArgumentException("Clover no está conectado.");

        var tzId = await _db.Affiliates.AsNoTracking().Where(a => a.Id == affiliateId).Select(a => a.Timezone).FirstOrDefaultAsync();
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(tzId) ? "America/New_York" : tzId); }
        catch { tz = TimeZoneInfo.Utc; }

        long Ms(DateOnly d) => new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MinValue), tz), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var startMs = Ms(from);
        var endMs = Ms(to.AddDays(1));

        var byDay = new Dictionary<string, (decimal total, int orders)>();
        var byTender = new Dictionary<string, (decimal total, int count)>();
        var items = new Dictionary<string, (int qty, decimal total)>();
        decimal gross = 0, tax = 0, tips = 0;
        var orders = 0;
        var truncated = false;

        try
        {
            for (var page = 0; page < MaxPages; page++)
            {
                var q = $"orders?expand=lineItems,payments,payments.tender&limit={PageSize}&offset={page * PageSize}"
                      + $"&filter={Uri.EscapeDataString($"createdTime>={startMs}")}&filter={Uri.EscapeDataString($"createdTime<{endMs}")}";
                using var doc = await GetAsync(c.ExternalId, token, q);
                if (!doc.RootElement.TryGetProperty("elements", out var els) || els.ValueKind != JsonValueKind.Array) break;
                var count = 0;
                foreach (var o in els.EnumerateArray())
                {
                    count++;
                    // Solo pagos exitosos: una orden abierta o cancelada no es venta.
                    decimal orderPaid = 0;
                    if (o.TryGetProperty("payments", out var pays) && pays.TryGetProperty("elements", out var pe))
                    {
                        foreach (var p in pe.EnumerateArray())
                        {
                            if (!p.TryGetProperty("result", out var res) || res.GetString() != "SUCCESS") continue;
                            var amt = Cents(p, "amount");
                            orderPaid += amt;
                            gross += amt;
                            tax += Cents(p, "taxAmount");
                            tips += Cents(p, "tipAmount");
                            var label = p.TryGetProperty("tender", out var t) && t.TryGetProperty("label", out var l) ? (l.GetString() ?? "Otro") : "Otro";
                            var cur = byTender.GetValueOrDefault(label);
                            byTender[label] = (cur.total + amt, cur.count + 1);
                        }
                    }
                    if (orderPaid <= 0) continue;
                    orders++;

                    var created = o.TryGetProperty("createdTime", out var ct) && ct.TryGetInt64(out var ms) ? ms : startMs;
                    var local = TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeMilliseconds(created).UtcDateTime, tz);
                    var day = local.ToString("yyyy-MM-dd");
                    var d = byDay.GetValueOrDefault(day);
                    byDay[day] = (d.total + orderPaid, d.orders + 1);

                    if (o.TryGetProperty("lineItems", out var lis) && lis.TryGetProperty("elements", out var le))
                    {
                        foreach (var li in le.EnumerateArray())
                        {
                            var name = li.TryGetProperty("name", out var nm) ? (nm.GetString() ?? "(sin nombre)") : "(sin nombre)";
                            var cur = items.GetValueOrDefault(name);
                            items[name] = (cur.qty + 1, cur.total + Cents(li, "price"));
                        }
                    }
                }
                if (count < PageSize) break;
                if (page == MaxPages - 1) truncated = true;
            }
        }
        catch (ArgumentException ex)
        {
            c.Status = "error";
            c.LastError = ex.Message;
            await _db.SaveChangesAsync();
            throw;
        }

        c.Status = "connected";
        c.LastError = null;
        c.LastSyncAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new CloverSalesReportDto(
            from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"), orders, gross, tax, tips,
            orders > 0 ? Math.Round(gross / orders, 2) : 0,
            byDay.OrderBy(x => x.Key).Select(x => new CloverDayDto(x.Key, x.Value.total, x.Value.orders)).ToList(),
            byTender.OrderByDescending(x => x.Value.total).Select(x => new CloverTenderDto(x.Key, x.Value.total, x.Value.count)).ToList(),
            items.OrderByDescending(x => x.Value.qty).Take(15).Select(x => new CloverTopItemDto(x.Key, x.Value.qty, x.Value.total)).ToList(),
            truncated);
    }

    private static decimal Cents(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.TryGetInt64(out var n) ? n / 100m : 0m;

    private async Task<JsonDocument> GetAsync(string merchantId, string token, string pathAndQuery)
    {
        var url = BaseUrl + merchantId + (pathAndQuery.Length == 0 ? "" : "/" + pathAndQuery);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var client = _http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        HttpResponseMessage res;
        try { res = await client.SendAsync(req); }
        catch (Exception) { throw new ArgumentException("No se pudo contactar a Clover. Intenta de nuevo."); }
        using (res)
        {
            if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                // Sin el token ni nada sensible: solo el código y el mensaje corto que devuelve Clover, para poder diagnosticar.
                var body = (await res.Content.ReadAsStringAsync()).Trim();
                if (body.Length > 160) body = body[..160];
                throw new ArgumentException($"Clover rechazó el token ({(int)res.StatusCode}: {body}). Revisa que sea de este comercio ({merchantId}) y tenga lectura de Inventory, Merchant, Orders y Payments.");
            }
            if (res.StatusCode == HttpStatusCode.NotFound)
                throw new ArgumentException("Clover no encontró ese Merchant ID.");
            if (!res.IsSuccessStatusCode)
                throw new ArgumentException($"Clover respondió {(int)res.StatusCode}. Intenta más tarde.");
            return JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        }
    }

    private static CloverConnectionDto ToDto(IntegrationConnection? c) =>
        c is null || string.IsNullOrEmpty(c.CredentialsEnc)
            ? new CloverConnectionDto(false, null, null, null, "disconnected", null, null)
            : new CloverConnectionDto(c.IsActive, c.ExternalId, c.ExternalName, c.CredentialsLast4, c.Status, c.LastError, c.LastSyncAt);
}
