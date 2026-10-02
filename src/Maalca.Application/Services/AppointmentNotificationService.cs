using System.Text;
using System.Text.Json;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maalca.Application.Services;

/// <summary>
/// Ver IAppointmentNotificationService. Falla en silencio (log + return) — un email que no
/// salió nunca debe tumbar la creación de una cita real, mismo criterio que OrderNotificationService.
/// </summary>
public class AppointmentNotificationService : IAppointmentNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AppointmentNotificationService> _logger;
    private readonly IAffiliateBrandResolver _brand;
    private readonly IOwnerNotificationService _owner;

    public AppointmentNotificationService(IHttpClientFactory httpClientFactory, ILogger<AppointmentNotificationService> logger, IAffiliateBrandResolver brand, IOwnerNotificationService owner)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _brand = brand;
        _owner = owner;
    }

    public async Task NotifyAppointmentBookedAsync(Appointment appointment, Customer customer, string businessName, string slug, string serviceName, string? staffName, string? zoomLink = null)
    {
        // Aviso al dueño (badge + push): cita nueva por confirmar. Antes del early-return del correo del cliente.
        var apptWhen = $"{appointment.Date:yyyy-MM-dd} {appointment.Time}";
        await _owner.NotifyAsync(
            appointment.AffiliateId, "appointment",
            "Nueva cita por confirmar",
            $"{customer.Name} · {serviceName} · {apptWhen}",
            "New appointment to confirm",
            $"{customer.Name} · {serviceName} · {apptWhen}",
            "agenda", appointment.Id);

        if (string.IsNullOrWhiteSpace(customer.Email))
            return; // sin correo del cliente no hay a quién notificar

        var baseUrl = Environment.GetEnvironmentVariable("MAALCA_WEB_URL");
        var secret = Environment.GetEnvironmentVariable("INTERNAL_NOTIFICATIONS_SECRET");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogInformation("[AppointmentNotification] Skipped — MAALCA_WEB_URL/INTERNAL_NOTIFICATIONS_SECRET not set");
            return;
        }

        try
        {
            var brand = await _brand.GetAsync(appointment.AffiliateId);
            var payload = new
            {
                logoUrl = brand.LogoUrl,
                brandColor = brand.Color,
                token = appointment.Token.ToString(),
                slug,
                businessName,
                customerEmail = customer.Email,
                customerName = customer.Name,
                serviceName,
                date = appointment.Date.ToString("yyyy-MM-dd"),
                time = appointment.Time,
                staffName,
                isVirtual = appointment.IsVirtual,
                zoomLink = appointment.IsVirtual ? zoomLink : null,
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/internal/notifications/appointment")
            {
                Content = content,
            };
            request.Headers.Add("X-Internal-Secret", secret);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[AppointmentNotification] Failed ({Status}): {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AppointmentNotification] Threw");
        }
    }

    public async Task NotifyAppointmentStatusAsync(Appointment appointment, Customer customer, string businessName, string slug, string serviceName, string? staffName, string kind, string? zoomLink = null)
    {
        if (string.IsNullOrWhiteSpace(customer.Email))
            return; // sin correo del cliente no hay a quién notificar

        var baseUrl = Environment.GetEnvironmentVariable("MAALCA_WEB_URL");
        var secret = Environment.GetEnvironmentVariable("INTERNAL_NOTIFICATIONS_SECRET");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogInformation("[AppointmentNotification] Status Skipped — MAALCA_WEB_URL/INTERNAL_NOTIFICATIONS_SECRET not set");
            return;
        }

        try
        {
            var brand = await _brand.GetAsync(appointment.AffiliateId);
            var payload = new
            {
                logoUrl = brand.LogoUrl,
                brandColor = brand.Color,
                kind,
                token = appointment.Token.ToString(),
                slug,
                businessName,
                customerEmail = customer.Email,
                customerName = customer.Name,
                serviceName,
                date = appointment.Date.ToString("yyyy-MM-dd"),
                time = appointment.Time,
                staffName,
                isVirtual = appointment.IsVirtual,
                zoomLink = appointment.IsVirtual ? zoomLink : null,
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/internal/notifications/appointment-status")
            {
                Content = content,
            };
            request.Headers.Add("X-Internal-Secret", secret);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[AppointmentNotification] Failed ({Status}): {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AppointmentNotification] Threw");
        }
    }
}
