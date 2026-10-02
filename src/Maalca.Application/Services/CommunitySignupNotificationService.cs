using System.Text;
using System.Text.Json;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maalca.Application.Services;

/// <summary>
/// Ver ICommunitySignupNotificationService. Falla en silencio (log + return), mismo criterio que
/// ReservationNotificationService: la inscripción ya quedó guardada, el correo es best-effort.
/// </summary>
public class CommunitySignupNotificationService : ICommunitySignupNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CommunitySignupNotificationService> _logger;
    private readonly IOwnerNotificationService _owner;

    public CommunitySignupNotificationService(IHttpClientFactory httpClientFactory, ILogger<CommunitySignupNotificationService> logger, IOwnerNotificationService owner)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _owner = owner;
    }

    public async Task NotifySignupCreatedAsync(CommunitySignup signup, Affiliate affiliate, Maalca.Domain.Entities.Activity? activity)
    {
        // Aviso al dueño (badge + push) antes de cualquier early-return del correo.
        if (signup.Kind == "event")
        {
            var who = signup.PartySize > 1 ? $"{signup.Name} (+{signup.PartySize - 1})" : signup.Name;
            await _owner.NotifyAsync(
                affiliate.Id, "signup",
                "Nueva inscripción a un evento",
                $"{who} · {signup.TargetTitle}",
                "New event registration",
                $"{who} · {signup.TargetTitle}",
                "inscripciones", signup.Id);
        }
        else
        {
            await _owner.NotifyAsync(
                affiliate.Id, "signup",
                "Nuevo voluntario",
                $"{signup.Name} quiere ayudar en: {signup.TargetTitle}",
                "New volunteer",
                $"{signup.Name} wants to help with: {signup.TargetTitle}",
                "inscripciones", signup.Id);
        }

        if (string.IsNullOrWhiteSpace(affiliate.ContactEmail) && string.IsNullOrWhiteSpace(signup.Email))
            return;

        await PostAsync("community-signup", BuildPayload(signup, affiliate, activity, null));
    }

    public async Task NotifySignupStatusAsync(CommunitySignup signup, Affiliate affiliate, Maalca.Domain.Entities.Activity? activity, string kind)
    {
        // Solo se le escribe a la persona; sin su correo no hay a quién avisar.
        if (string.IsNullOrWhiteSpace(signup.Email))
            return;

        await PostAsync("community-signup-status", BuildPayload(signup, affiliate, activity, kind));
    }

    private static object BuildPayload(CommunitySignup signup, Affiliate affiliate, Maalca.Domain.Entities.Activity? activity, string? statusKind)
    {
        return new
        {
            kind = statusKind,
            signupKind = signup.Kind,
            status = signup.Status,
            language = signup.Language,
            businessName = affiliate.Name,
            businessEmail = affiliate.ContactEmail,
            businessPhone = affiliate.WhatsApp,
            slug = affiliate.Slug,
            logoUrl = string.IsNullOrWhiteSpace(affiliate.LogoUrl) ? affiliate.Logo : affiliate.LogoUrl,
            brandColor = affiliate.PrimaryColor,
            timezone = affiliate.Timezone,
            name = signup.Name,
            phone = signup.Phone,
            email = signup.Email,
            partySize = signup.PartySize,
            notes = signup.Notes,
            targetTitle = signup.TargetTitle,
            eventStartsAt = activity?.StartsAt,
            eventEndsAt = activity?.EndsAt,
            eventLocation = activity?.Location,
        };
    }

    private async Task PostAsync(string route, object payload)
    {
        var baseUrl = Environment.GetEnvironmentVariable("MAALCA_WEB_URL");
        var secret = Environment.GetEnvironmentVariable("INTERNAL_NOTIFICATIONS_SECRET");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogInformation("[CommunitySignupNotification] Skipped — MAALCA_WEB_URL/INTERNAL_NOTIFICATIONS_SECRET not set");
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/internal/notifications/{route}")
            {
                Content = content,
            };
            request.Headers.Add("X-Internal-Secret", secret);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[CommunitySignupNotification] Failed ({Status}): {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CommunitySignupNotification] Threw");
        }
    }
}
