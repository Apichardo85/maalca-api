using System.Text;
using System.Text.Json;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maalca.Application.Services;

/// <summary>
/// Ver IOwnerNotificationService. El envío del push vive en maalca-web (librería web-push + llaves
/// VAPID) y se le llama por el mismo endpoint interno protegido por secreto que usan los correos:
/// aquí solo guardamos avisos y suscripciones.
/// </summary>
public class OwnerNotificationService : IOwnerNotificationService
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "order", "reservation", "appointment", "invoice_paid", "proposal_accepted"
    };

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OwnerNotificationService> _logger;

    public OwnerNotificationService(AppDbContext db, IHttpClientFactory httpClientFactory, ILogger<OwnerNotificationService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task NotifyAsync(Guid affiliateId, string type, string title, string? body, string titleEn, string? bodyEn, string? url, Guid? entityId)
    {
        try
        {
            if (!ValidTypes.Contains(type)) return;

            _db.Set<OwnerNotification>().Add(new OwnerNotification
            {
                AffiliateId = affiliateId,
                Type = type.ToLowerInvariant(),
                Title = Truncate(title, 200),
                Body = body is null ? null : Truncate(body, 500),
                TitleEn = Truncate(titleEn, 200),
                BodyEn = bodyEn is null ? null : Truncate(bodyEn, 500),
                Url = url,
                EntityId = entityId,
            });
            await _db.SaveChangesAsync();

            await SendPushAsync(affiliateId, type, title, body, titleEn, bodyEn, url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OwnerNotification] Threw ({Type})", type);
        }
    }

    private async Task SendPushAsync(Guid affiliateId, string type, string title, string? body, string titleEn, string? bodyEn, string? url)
    {
        var baseUrl = Environment.GetEnvironmentVariable("MAALCA_WEB_URL");
        var secret = Environment.GetEnvironmentVariable("INTERNAL_NOTIFICATIONS_SECRET");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
            return;

        var subs = await _db.Set<PushSubscription>().Where(s => s.AffiliateId == affiliateId).ToListAsync();
        if (subs.Count == 0) return;

        var slug = await _db.Affiliates.Where(a => a.Id == affiliateId).Select(a => a.Slug).FirstOrDefaultAsync();

        var payload = new
        {
            subscriptions = subs.Select(s => new { endpoint = s.Endpoint, p256dh = s.P256dh, auth = s.Auth, lang = s.Lang }),
            message = new { type, title, body, titleEn, bodyEn, slug, url },
        };

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/internal/notifications/push")
        {
            Content = content,
        };
        request.Headers.Add("X-Internal-Secret", secret);

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[OwnerNotification] Push failed ({Status})", response.StatusCode);
            return;
        }

        // La web responde con los endpoints que el servicio de push dio por muertos (404/410): se borran.
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("expired", out var expiredEl) && expiredEl.ValueKind == JsonValueKind.Array)
        {
            var expired = expiredEl.EnumerateArray().Select(e => e.GetString()).Where(e => !string.IsNullOrEmpty(e)).ToList();
            if (expired.Count > 0)
            {
                var dead = subs.Where(s => expired.Contains(s.Endpoint)).ToList();
                _db.Set<PushSubscription>().RemoveRange(dead);
                await _db.SaveChangesAsync();
            }
        }
    }

    public async Task<IReadOnlyList<OwnerNotificationDto>> ListAsync(Guid affiliateId, int take)
    {
        take = Math.Clamp(take, 1, 100);
        var rows = await _db.Set<OwnerNotification>()
            .Where(n => n.AffiliateId == affiliateId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync();
        return rows.Select(n => new OwnerNotificationDto(
            n.Id, n.Type, n.Title, n.Body, n.TitleEn, n.BodyEn, n.Url, n.EntityId, n.CreatedAt, n.ReadAt != null)).ToList();
    }

    public async Task<OwnerNotificationSummaryDto> SummaryAsync(Guid affiliateId)
    {
        var groups = await _db.Set<OwnerNotification>()
            .Where(n => n.AffiliateId == affiliateId && n.ReadAt == null)
            .GroupBy(n => n.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();
        var byType = groups.ToDictionary(g => g.Type, g => g.Count);
        return new OwnerNotificationSummaryDto(byType.Values.Sum(), byType);
    }

    public async Task MarkReadAsync(Guid affiliateId, MarkNotificationsReadRequest request)
    {
        var query = _db.Set<OwnerNotification>().Where(n => n.AffiliateId == affiliateId && n.ReadAt == null);
        if (request.Ids is { Count: > 0 })
            query = query.Where(n => request.Ids.Contains(n.Id));
        else if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var type = request.Type.Trim().ToLowerInvariant();
            query = query.Where(n => n.Type == type);
        }

        var now = DateTime.UtcNow;
        var rows = await query.ToListAsync();
        foreach (var n in rows) n.ReadAt = now;
        if (rows.Count > 0) await _db.SaveChangesAsync();
    }

    public async Task SubscribeAsync(Guid affiliateId, PushSubscribeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint) || request.Endpoint.Length > 1000
            || string.IsNullOrWhiteSpace(request.P256dh) || request.P256dh.Length > 300
            || string.IsNullOrWhiteSpace(request.Auth) || request.Auth.Length > 100)
            throw new ArgumentException("Suscripción de push inválida.");

        var lang = request.Lang == "en" ? "en" : "es";
        var existing = await _db.Set<PushSubscription>().FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint);
        if (existing is null)
        {
            _db.Set<PushSubscription>().Add(new PushSubscription
            {
                AffiliateId = affiliateId,
                Endpoint = request.Endpoint,
                P256dh = request.P256dh,
                Auth = request.Auth,
                Lang = lang,
            });
        }
        else
        {
            // El mismo navegador se re-suscribe (o cambió de cuenta): se queda con la fila, con los datos nuevos.
            existing.AffiliateId = affiliateId;
            existing.P256dh = request.P256dh;
            existing.Auth = request.Auth;
            existing.Lang = lang;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    public async Task UnsubscribeAsync(Guid affiliateId, string endpoint)
    {
        var rows = await _db.Set<PushSubscription>().Where(s => s.AffiliateId == affiliateId && s.Endpoint == endpoint).ToListAsync();
        if (rows.Count == 0) return;
        _db.Set<PushSubscription>().RemoveRange(rows);
        await _db.SaveChangesAsync();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
