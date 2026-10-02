using System.Text;
using System.Text.Json;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maalca.Application.Services;

/// <summary>
/// Ver IReservationNotificationService. Falla en silencio (log + return), mismo criterio que
/// InvoiceNotificationService: el email es best-effort, la reserva ya quedó guardada.
/// </summary>
public class ReservationNotificationService : IReservationNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReservationNotificationService> _logger;
    private readonly IOwnerNotificationService _owner;

    public ReservationNotificationService(IHttpClientFactory httpClientFactory, ILogger<ReservationNotificationService> logger, IOwnerNotificationService owner)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _owner = owner;
    }

    public async Task NotifyReservationRequestedAsync(TableReservation reservation, Affiliate affiliate)
    {
        // Aviso al dueño (badge + push): reserva por aceptar o rechazar. Antes del early-return del correo.
        var resWhen = $"{reservation.Date:yyyy-MM-dd} {reservation.Time}";
        await _owner.NotifyAsync(
            affiliate.Id, "reservation",
            "Nueva reserva por confirmar",
            $"{reservation.CustomerName} · {reservation.PartySize} personas · {resWhen}",
            "New reservation to confirm",
            $"{reservation.CustomerName} · party of {reservation.PartySize} · {resWhen}",
            "reservations", reservation.Id);

        // Sin ningún destinatario (ni correo del negocio ni del comensal) no hay nada que mandar.
        if (string.IsNullOrWhiteSpace(affiliate.ContactEmail) && string.IsNullOrWhiteSpace(reservation.CustomerEmail))
            return;

        var baseUrl = Environment.GetEnvironmentVariable("MAALCA_WEB_URL");
        var secret = Environment.GetEnvironmentVariable("INTERNAL_NOTIFICATIONS_SECRET");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogInformation("[ReservationNotification] Skipped — MAALCA_WEB_URL/INTERNAL_NOTIFICATIONS_SECRET not set");
            return;
        }

        try
        {
            var payload = new
            {
                businessName = affiliate.Name,
                businessEmail = affiliate.ContactEmail,
                slug = affiliate.Slug,
                logoUrl = string.IsNullOrWhiteSpace(affiliate.LogoUrl) ? affiliate.Logo : affiliate.LogoUrl,
                brandColor = affiliate.PrimaryColor,
                customerName = reservation.CustomerName,
                customerPhone = reservation.CustomerPhone,
                customerEmail = reservation.CustomerEmail,
                date = reservation.Date.ToString("yyyy-MM-dd"),
                time = reservation.Time,
                partySize = reservation.PartySize,
                notes = reservation.Notes,
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/internal/notifications/reservation-requested")
            {
                Content = content,
            };
            request.Headers.Add("X-Internal-Secret", secret);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[ReservationNotification] Failed ({Status}): {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ReservationNotification] Threw");
        }
    }

    public async Task NotifyReservationStatusAsync(TableReservation reservation, Affiliate affiliate, string kind)
    {
        // Solo se le escribe al comensal; sin su correo no hay a quién avisar.
        if (string.IsNullOrWhiteSpace(reservation.CustomerEmail))
            return;

        var baseUrl = Environment.GetEnvironmentVariable("MAALCA_WEB_URL");
        var secret = Environment.GetEnvironmentVariable("INTERNAL_NOTIFICATIONS_SECRET");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogInformation("[ReservationNotification] Status Skipped — MAALCA_WEB_URL/INTERNAL_NOTIFICATIONS_SECRET not set");
            return;
        }

        try
        {
            var payload = new
            {
                kind,
                businessName = affiliate.Name,
                businessEmail = affiliate.ContactEmail,
                slug = affiliate.Slug,
                logoUrl = string.IsNullOrWhiteSpace(affiliate.LogoUrl) ? affiliate.Logo : affiliate.LogoUrl,
                brandColor = affiliate.PrimaryColor,
                customerName = reservation.CustomerName,
                customerPhone = reservation.CustomerPhone,
                customerEmail = reservation.CustomerEmail,
                date = reservation.Date.ToString("yyyy-MM-dd"),
                time = reservation.Time,
                partySize = reservation.PartySize,
                notes = reservation.Notes,
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/internal/notifications/reservation-status")
            {
                Content = content,
            };
            request.Headers.Add("X-Internal-Secret", secret);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[ReservationNotification] Failed ({Status}): {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ReservationNotification] Threw");
        }
    }
}
