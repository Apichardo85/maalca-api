using System.Text;
using System.Text.Json;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maalca.Application.Services;

/// <summary>
/// Ver IInvoiceNotificationService. Falla en silencio (log + return) — un email que no salió
/// nunca debe tumbar la generación real del link de cobro, mismo criterio que
/// AppointmentNotificationService/OrderNotificationService.
/// </summary>
public class InvoiceNotificationService : IInvoiceNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<InvoiceNotificationService> _logger;

    public InvoiceNotificationService(IHttpClientFactory httpClientFactory, ILogger<InvoiceNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task NotifyInvoicePaymentLinkAsync(Invoice invoice, Customer customer, string businessName, string currency, string paymentLink)
    {
        if (string.IsNullOrWhiteSpace(customer.Email))
            return; // sin correo del cliente no hay a quién notificar — el link se puede copiar/mandar por WhatsApp igual

        var baseUrl = Environment.GetEnvironmentVariable("MAALCA_WEB_URL");
        var secret = Environment.GetEnvironmentVariable("INTERNAL_NOTIFICATIONS_SECRET");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogInformation("[InvoiceNotification] Skipped — MAALCA_WEB_URL/INTERNAL_NOTIFICATIONS_SECRET not set");
            return;
        }

        try
        {
            var payload = new
            {
                customerEmail = customer.Email,
                customerName = customer.Name,
                businessName,
                invoiceNumber = invoice.InvoiceNumber,
                total = invoice.Total,
                currency,
                paymentLink,
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/internal/notifications/invoice")
            {
                Content = content,
            };
            request.Headers.Add("X-Internal-Secret", secret);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[InvoiceNotification] Failed ({Status}): {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[InvoiceNotification] Threw");
        }
    }
}
