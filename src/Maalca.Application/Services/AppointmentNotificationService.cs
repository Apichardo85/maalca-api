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

    public AppointmentNotificationService(IHttpClientFactory httpClientFactory, ILogger<AppointmentNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task NotifyAppointmentBookedAsync(Appointment appointment, Customer customer, string businessName, string slug, string serviceName, string? staffName, string? zoomLink = null)
    {
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
            var payload = new
            {
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
}
