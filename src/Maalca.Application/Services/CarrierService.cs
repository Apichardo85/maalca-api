using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Maalca.Application.Services;

/// <summary>
/// United Bridge (https://united-bridge.com/docs/api): POST labels/rate (tarifa real), POST labels/buy (etiqueta + tracking).
/// Sin sandbox: cada compra es real y se descuenta del saldo prepagado de la cuenta del negocio. Una compra exitosa se guarda
/// en el pedido ANTES de marcarlo enviado, para no perder nunca una etiqueta pagada.
/// </summary>
public class CarrierService : ICarrierService
{
    private const string Provider = "unitedbridge";
    private const string BaseUrl = "https://united-bridge.com/api/labels/";
    private static readonly Regex ServiceCode = new("^[A-Z0-9_\\-]{2,40}$", RegexOptions.Compiled);
    private static readonly Regex PackageCode = new("^[A-Za-z0-9_\\-]{2,40}$", RegexOptions.Compiled);
    private static readonly Regex Zip = new("^\\d{5}(-\\d{4})?$", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly IOrderService _orders;
    private readonly string _secret;

    public CarrierService(AppDbContext db, IHttpClientFactory http, IOrderService orders, IConfiguration config)
    {
        _db = db;
        _http = http;
        _orders = orders;
        _secret = config["Jwt:Key"] ?? "MaalcaSecretKey12345678901234567890";
    }

    // ── Cuenta ───────────────────────────────────────────────────────────────

    public async Task<CarrierAccountDto> GetAccountAsync(Guid affiliateId)
    {
        var a = await _db.CarrierAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.AffiliateId == affiliateId && x.Provider == Provider);
        return ToDto(a ?? new CarrierAccount { AffiliateId = affiliateId });
    }

    public async Task<CarrierAccountDto> UpsertAccountAsync(Guid affiliateId, UpsertCarrierAccountRequest r)
    {
        var service = (r.Service ?? "").Trim().ToUpperInvariant();
        if (!ServiceCode.IsMatch(service)) throw new ArgumentException("Servicio inválido. Ej.: UPS_GROUND, FEDEX_HOME_DELIVERY, USPS_UB_GA o BEST_UB_SERVICE.");
        var package = (r.Package ?? "").Trim();
        if (!PackageCode.IsMatch(package)) throw new ArgumentException("Tipo de empaque inválido. Ej.: Parcel, YOUR_PACKAGING o BEST_UB_PACKAGE.");
        foreach (var d in new[] { r.LengthIn, r.WidthIn, r.HeightIn })
            if (d <= 0 || d > 60) throw new ArgumentException("Las medidas de la caja deben estar entre 0 y 60 pulgadas.");
        if (r.BoxWeightOz < 0 || r.BoxWeightOz > 1600) throw new ArgumentException("El peso de la caja no es válido.");
        var state = r.FromState?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(state) && !Regex.IsMatch(state, "^[A-Z]{2}$")) throw new ArgumentException("El estado de origen debe ser de 2 letras (NJ).");
        var zip = r.FromZip?.Trim();
        if (!string.IsNullOrEmpty(zip) && !Zip.IsMatch(zip)) throw new ArgumentException("El ZIP de origen no es válido.");

        var a = await _db.CarrierAccounts.FirstOrDefaultAsync(x => x.AffiliateId == affiliateId && x.Provider == Provider);
        if (a is null)
        {
            a = new CarrierAccount { AffiliateId = affiliateId, Provider = Provider };
            _db.CarrierAccounts.Add(a);
        }
        var key = r.ApiKey?.Trim();
        if (!string.IsNullOrEmpty(key))
        {
            if (key.Length < 12 || key.Length > 200 || key.Any(char.IsWhiteSpace)) throw new ArgumentException("La API key no parece válida.");
            a.ApiKeyEnc = SecretBox.Protect(key, _secret);
            a.ApiKeyLast4 = key[^4..];
        }
        a.Service = service;
        a.Package = package;
        a.LengthIn = r.LengthIn;
        a.WidthIn = r.WidthIn;
        a.HeightIn = r.HeightIn;
        a.BoxWeightOz = r.BoxWeightOz;
        a.FromName = Clean(r.FromName, 80);
        a.FromPhone = Clean(r.FromPhone, 30);
        a.FromAddress1 = Clean(r.FromAddress1, 120);
        a.FromAddress2 = Clean(r.FromAddress2, 120);
        a.FromCity = Clean(r.FromCity, 80);
        a.FromState = string.IsNullOrEmpty(state) ? null : state;
        a.FromZip = string.IsNullOrEmpty(zip) ? null : zip;
        a.IsActive = r.IsActive;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(a);
    }

    // ── Tarifa real y compra ─────────────────────────────────────────────────

    public async Task<LabelQuoteDto?> QuoteAsync(Guid affiliateId, Guid orderId, decimal? weightOz)
    {
        var (order, account, key) = await LoadAsync(affiliateId, orderId, forBuy: false);
        if (order is null) return null;
        var weight = await WeightAsync(order, account!, weightOz);
        using var doc = await PostAsync("rate/", key!, Body(order, account!, weight), isPurchase: false);
        var root = doc.RootElement;
        var total = Dec(root, "rate", "total");
        return new LabelQuoteDto(account!.Service, total, DecOr(root, total, "rate", "shipment_rate"),
            Str(root, "currency") ?? order.Currency, DecN(root, "account_balance"), weight, order.ShippingCost);
    }

    public async Task<BuyLabelResultDto?> BuyLabelAsync(Guid affiliateId, Guid orderId, decimal? weightOz)
    {
        var (order, account, key) = await LoadAsync(affiliateId, orderId, forBuy: true);
        if (order is null) return null;
        var weight = await WeightAsync(order, account!, weightOz);

        using var doc = await PostAsync("buy/", key!, Body(order, account!, weight), isPurchase: true);
        var root = doc.RootElement;
        var labelUrl = Str(root, "postage_url");
        var tracking = Str(root, "tracking_number");
        if (string.IsNullOrEmpty(tracking))
            throw new ArgumentException("United Bridge respondió sin número de rastreo. Revisa tus Transacciones en United Bridge antes de reintentar.");
        var cost = Dec(root, "rate", "total");

        // La etiqueta ya está pagada: guardarla primero, pase lo que pase después.
        var carrier = CarrierOf(account!.Service);
        order.LabelUrl = labelUrl;
        order.LabelCost = cost;
        order.ShippingCarrier = carrier;
        order.TrackingNumber = tracking;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var dto = await _orders.MarkShippedAsync(affiliateId, orderId, carrier, tracking);
        if (dto is null) return null;
        return new BuyLabelResultDto(dto, labelUrl ?? "", cost, DecN(root, "account_balance"));
    }

    // ── Internos ─────────────────────────────────────────────────────────────

    private async Task<(Order? order, CarrierAccount? account, string? key)> LoadAsync(Guid affiliateId, Guid orderId, bool forBuy)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.AffiliateId == affiliateId);
        if (order is null) return (null, null, null);
        if (order.FulfillmentType != OrderService.ShippingFulfillment) throw new ArgumentException("Este pedido no es para envío.");
        if (order.Status is OrderStatus.Pending or OrderStatus.Canceled) throw new ArgumentException("Solo se despachan pedidos pagados.");
        if (forBuy && (!string.IsNullOrEmpty(order.LabelUrl) || !string.IsNullOrEmpty(order.TrackingNumber)))
            throw new ArgumentException("Este pedido ya tiene etiqueta o número de rastreo.");
        if (string.IsNullOrWhiteSpace(order.ShipToLine1) || string.IsNullOrWhiteSpace(order.ShipToCity) ||
            string.IsNullOrWhiteSpace(order.ShipToState) || string.IsNullOrWhiteSpace(order.ShipToZip))
            throw new ArgumentException("El pedido no tiene dirección de envío completa.");

        var a = await _db.CarrierAccounts.FirstOrDefaultAsync(x => x.AffiliateId == affiliateId && x.Provider == Provider);
        if (a is null || !a.IsActive || string.IsNullOrEmpty(a.ApiKeyEnc))
            throw new ArgumentException("Falta conectar United Bridge: Envíos → Etiquetas.");
        if (string.IsNullOrWhiteSpace(a.FromAddress1) || string.IsNullOrWhiteSpace(a.FromCity) ||
            string.IsNullOrWhiteSpace(a.FromState) || string.IsNullOrWhiteSpace(a.FromZip) || string.IsNullOrWhiteSpace(a.FromName))
            throw new ArgumentException("Completa la dirección de origen en Envíos → Etiquetas.");
        var key = SecretBox.Unprotect(a.ApiKeyEnc, _secret);
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("No se pudo leer la API key guardada. Vuelve a pegarla en Envíos → Etiquetas.");
        return (order, a, key);
    }

    /// <summary>Peso del paquete en onzas = override (o suma de los productos) + caja.</summary>
    private async Task<decimal> WeightAsync(Order order, CarrierAccount account, decimal? overrideOz)
    {
        decimal products;
        if (overrideOz is > 0) products = overrideOz.Value;
        else
        {
            var lines = JsonArrayField.Parse<OrderItemDto>(order.ItemsJson);
            var ids = lines.Select(l => Guid.TryParse(l.ItemId, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().ToList();
            var weights = await _db.InventoryItems.AsNoTracking().Where(i => ids.Contains(i.Id))
                .Select(i => new { i.Id, i.WeightOz }).ToDictionaryAsync(i => i.Id, i => i.WeightOz ?? 0m);
            products = lines.Sum(l => Guid.TryParse(l.ItemId, out var g) && weights.TryGetValue(g, out var w) ? w * l.Qty : 0m);
        }
        if (products <= 0) throw new ArgumentException("Los productos de este pedido no tienen peso. Escribe el peso del paquete a mano.");
        var total = products + account.BoxWeightOz;
        if (total > 2400) throw new ArgumentException("El peso del paquete no es válido.");
        return Math.Round(total, 2);
    }

    private static object Body(Order o, CarrierAccount a, decimal weightOz) => new
    {
        sender = new { name = a.FromName, address1 = a.FromAddress1, address2 = a.FromAddress2, city = a.FromCity, state = a.FromState, postal_code = a.FromZip, country = "US", phone = a.FromPhone },
        recipient = new { name = string.IsNullOrWhiteSpace(o.ShipToName) ? (o.CustomerName ?? "Cliente") : o.ShipToName, address1 = o.ShipToLine1, address2 = o.ShipToLine2, city = o.ShipToCity, state = o.ShipToState, postal_code = o.ShipToZip, country = "US", phone = o.CustomerPhone },
        weight = weightOz,
        weight_unit = "oz",
        dimensions = new { length = a.LengthIn, width = a.WidthIn, height = a.HeightIn },
        dimensions_unit = "in",
        service = a.Service,
        package = a.Package,
        metadata = new { reference1 = o.Id.ToString("N")[..8].ToUpperInvariant() },
    };

    /// <summary>La documentación dice Bearer pero muestra el header sin prefijo: se prueba con Bearer y, si rechaza la credencial, sin él.</summary>
    private async Task<JsonDocument> PostAsync(string path, string apiKey, object body, bool isPurchase)
    {
        var client = _http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(60);
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        foreach (var prefix in new[] { "Bearer ", "" })
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path);
            req.Headers.TryAddWithoutValidation("Authorization", prefix + apiKey);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage res;
            string text;
            try
            {
                res = await client.SendAsync(req);
                text = await res.Content.ReadAsStringAsync();
            }
            catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
            {
                throw new ArgumentException(isPurchase
                    ? "No se pudo confirmar la compra con United Bridge. Revisa tus Transacciones en United Bridge ANTES de reintentar para no pagar dos veces."
                    : "No se pudo conectar con United Bridge. Intenta de nuevo.");
            }
            using (res)
            {
                if ((int)res.StatusCode is 401 or 403 && prefix.Length > 0) continue;
                if ((int)res.StatusCode is 401 or 403)
                    throw new ArgumentException("United Bridge rechazó la API key. Genera una nueva en su cuenta y pégala en Envíos → Etiquetas.");
                if (!res.IsSuccessStatusCode)
                    throw new ArgumentException("United Bridge: " + (text.Length > 300 ? text[..300] : text));
                try { return JsonDocument.Parse(text); }
                catch (JsonException) { throw new ArgumentException("United Bridge respondió algo inesperado."); }
            }
        }
        throw new ArgumentException("United Bridge rechazó la API key.");
    }

    private static string CarrierOf(string service) =>
        service.StartsWith("UPS") ? "ups" : service.StartsWith("FEDEX") ? "fedex" : service.StartsWith("USPS") ? "usps" : service.StartsWith("DHL") ? "dhl" : "other";

    private static string? Clean(string? s, int max)
    {
        var t = s?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length > max ? t[..max] : t;
    }

    private static CarrierAccountDto ToDto(CarrierAccount a) => new(
        !string.IsNullOrEmpty(a.ApiKeyEnc), a.ApiKeyLast4, a.Service, a.Package, a.LengthIn, a.WidthIn, a.HeightIn, a.BoxWeightOz,
        a.FromName, a.FromPhone, a.FromAddress1, a.FromAddress2, a.FromCity, a.FromState, a.FromZip, a.IsActive);

    private static JsonElement? Walk(JsonElement e, string[] path)
    {
        foreach (var p in path)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(p, out var next)) return null;
            e = next;
        }
        return e;
    }

    private static decimal? DecN(JsonElement e, params string[] path)
    {
        var v = Walk(e, path);
        if (v is null) return null;
        if (v.Value.ValueKind == JsonValueKind.Number && v.Value.TryGetDecimal(out var n)) return n;
        if (v.Value.ValueKind == JsonValueKind.String && decimal.TryParse(v.Value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var s)) return s;
        return null;
    }

    private static decimal Dec(JsonElement e, params string[] path) =>
        DecN(e, path) ?? throw new ArgumentException("United Bridge respondió sin el costo del envío.");

    private static decimal DecOr(JsonElement e, decimal fallback, params string[] path) => DecN(e, path) ?? fallback;

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
