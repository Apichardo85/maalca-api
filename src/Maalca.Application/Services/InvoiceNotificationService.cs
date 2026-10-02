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
    private readonly IAffiliateBrandResolver _brand;

    public InvoiceNotificationService(IHttpClientFactory httpClientFactory, ILogger<InvoiceNotificationService> logger, IAffiliateBrandResolver brand)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _brand = brand;
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
            var brand = await _brand.GetAsync(invoice.AffiliateId);
            var payload = new
            {
                logoUrl = brand.LogoUrl,
                brandColor = brand.Color,
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

    public async Task NotifyInvoicePaidAsync(Invoice invoice, Customer customer, string businessName, string currency)
    {
        if (string.IsNullOrWhiteSpace(customer.Email))
            return; // sin correo del cliente no hay a quien mandarle el recibo

        var baseUrl = Environment.GetEnvironmentVariable("MAALCA_WEB_URL");
        var secret = Environment.GetEnvironmentVariable("INTERNAL_NOTIFICATIONS_SECRET");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogInformation("[InvoiceNotification] Paid-notify skipped — MAALCA_WEB_URL/INTERNAL_NOTIFICATIONS_SECRET not set");
            return;
        }

        try
        {
            var brand = await _brand.GetAsync(invoice.AffiliateId);
            var payload = new
            {
                logoUrl = brand.LogoUrl,
                brandColor = brand.Color,
                customerEmail = customer.Email,
                customerName = customer.Name,
                businessName,
                invoiceNumber = invoice.InvoiceNumber,
                total = invoice.Total,
                currency,
                paidDate = invoice.PaidDate,
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/internal/notifications/invoice-paid")
            {
                Content = content,
            };
            request.Headers.Add("X-Internal-Secret", secret);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[InvoiceNotification] Paid-notify failed ({Status}): {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[InvoiceNotification] Paid-notify threw");
        }
    }
}
