using System.Text;
using System.Text.Json;
using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Services;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Api.Workers;

/// <summary>
/// Vigila las TVs (ScreenDevice) cada minuto y avisa a MaalCa por email:
///  - "caída": una TV no retirada lleva más de 5 min sin señal y el negocio está abierto
///    (Horario + Timezone del afiliado; si no hay horario se vigila siempre). Exige que el negocio
///    lleve abierto al menos 15 min, para no alertar por la TV que se enciende al abrir.
///  - "volvió": una TV con aviso abierto vuelve a mandar señal.
/// Un aviso por incidente (AlertSentAt). El correo lo manda maalca-web por el endpoint interno
/// (mismo secreto que el resto de notificaciones). Destinatarios: SCREEN_ALERT_EMAILS (coma) o, si
/// no está, los PlatformAdmins con rol Owner — por ahora las alertas son solo para MaalCa.
/// </summary>
public class ScreenMonitorWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan OpenGrace = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan IgnoreOlderThan = TimeSpan.FromDays(7);

    private readonly IServiceScopeFactory _scopes;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<ScreenMonitorWorker> _logger;

    public ScreenMonitorWorker(IServiceScopeFactory scopes, IHttpClientFactory http, ILogger<ScreenMonitorWorker> logger)
    {
        _scopes = scopes;
        _http = http;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Margen al arrancar: tras un deploy las TVs tardan hasta un minuto en volver a reportar.
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogWarning(ex, "[ScreenMonitor] ciclo falló"); }
            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var cutoff = now - ScreenDeviceService.OfflineAfter;
        var oldest = now - IgnoreOlderThan;

        var downCandidates = await db.ScreenDevices.Include(d => d.Affiliate)
            .Where(d => !d.Archived && d.AlertSentAt == null && d.LastSeenAt < cutoff && d.LastSeenAt > oldest)
            .ToListAsync(ct);
        var down = downCandidates.Where(d => ShouldBeOn(d.Affiliate, now)).ToList();

        var recovered = await db.ScreenDevices.Include(d => d.Affiliate)
            .Where(d => d.AlertSentAt != null && d.LastSeenAt > d.AlertSentAt)
            .ToListAsync(ct);

        if (down.Count == 0 && recovered.Count == 0) return;

        var screenIds = down.Concat(recovered).Where(d => d.ScreenId.HasValue).Select(d => d.ScreenId!.Value).Distinct().ToList();
        var names = await db.Screens.Where(s => screenIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        string ScreenName(ScreenDevice d) => d.ScreenId is Guid sid ? (names.TryGetValue(sid, out var n) ? n : "Pantalla borrada") : ScreenDeviceService.MainScreenName;

        var events = down.Select(d => Event("down", d, ScreenName(d)))
            .Concat(recovered.Select(d => Event("up", d, ScreenName(d))))
            .ToList();

        var to = await RecipientsAsync(db, ct);
        if (to.Count == 0)
        {
            _logger.LogWarning("[ScreenMonitor] {Count} aviso(s) sin destinatarios (ni SCREEN_ALERT_EMAILS ni PlatformAdmins Owner)", events.Count);
            return;
        }

        if (!await SendAsync(to, events, ct)) return; // se reintenta en el próximo ciclo

        foreach (var d in down) d.AlertSentAt = now;
        foreach (var d in recovered) d.AlertSentAt = null;
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("[ScreenMonitor] avisos enviados: {Down} caída(s), {Up} recuperada(s)", down.Count, recovered.Count);
    }

    private static object Event(string kind, ScreenDevice d, string screenName) => new
    {
        kind,
        businessName = d.Affiliate?.Name ?? "",
        slug = d.Affiliate?.Slug ?? "",
        screenName,
        deviceId = d.DeviceId,
        lastSeenAt = d.LastSeenAt,
        showing = d.ShowingLabel,
        timezone = d.Affiliate?.Timezone,
    };

    /// ¿La TV debería estar encendida ahora? Abierto ahora y hace 15 min. Sin horario conocido = sí.
    private static bool ShouldBeOn(Affiliate? a, DateTime now)
    {
        if (a is null) return false;
        var horario = JsonArrayField.Parse<HorarioEntryDto>(a.Horario);
        var nowCheck = BusinessHoursCheck.Evaluate(horario, a.Timezone, now);
        if (!nowCheck.Known) return true;
        if (!nowCheck.IsOpen) return false;
        return BusinessHoursCheck.Evaluate(horario, a.Timezone, now - OpenGrace).IsOpen;
    }

    private static async Task<List<string>> RecipientsAsync(AppDbContext db, CancellationToken ct)
    {
        var env = Environment.GetEnvironmentVariable("SCREEN_ALERT_EMAILS");
        if (!string.IsNullOrWhiteSpace(env))
            return env.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        return await db.Set<PlatformAdmin>().Where(p => p.Role == PlatformAdminRole.Owner).Select(p => p.Email).ToListAsync(ct);
    }

    private async Task<bool> SendAsync(List<string> to, List<object> events, CancellationToken ct)
    {
        var baseUrl = Environment.GetEnvironmentVariable("MAALCA_WEB_URL");
        var secret = Environment.GetEnvironmentVariable("INTERNAL_NOTIFICATIONS_SECRET");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogWarning("[ScreenMonitor] MAALCA_WEB_URL / INTERNAL_NOTIFICATIONS_SECRET no configurados: aviso no enviado");
            return false;
        }
        var client = _http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        using var content = new StringContent(JsonSerializer.Serialize(new { to, events }), Encoding.UTF8, "application/json");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/internal/notifications/screen-alert") { Content = content };
        req.Headers.Add("X-Internal-Secret", secret);
        var res = await client.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("[ScreenMonitor] la web respondió {Status} al enviar avisos", res.StatusCode);
            return false;
        }
        return true;
    }
}
