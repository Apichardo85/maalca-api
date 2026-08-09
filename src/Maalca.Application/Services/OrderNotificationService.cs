using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maalca.Application.Services;

/// <summary>
/// Ver IOrderNotificationService. Falla en silencio (log + return) — un email que no salió
/// nunca debe tumbar la confirmación de un pago real ni el cambio de estado de un pedido.
/// </summary>
public class OrderNotificationService : IOrderNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OrderNotificationService> _logger;

    public OrderNotificationService(IHttpClientFactory httpClientFactory, ILogger<OrderNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task NotifyOrderConfirmedAsync(Order order) => SendAsync(order, "confirmed");

    public Task NotifyOrderFulfilledAsync(Order order) => SendAsync(order, "fulfilled");

    private async Task SendAsync(Order order, string kind)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerEmail))
            return; // sin correo del cliente no hay a quién notificar

        var baseUrl = Environment.GetEnvironmentVariable("MAALCA_WEB_URL");
        var secret = Environment.GetEnvironmentVariable("INTERNAL_NOTIFICATIONS_SECRET");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogInformation("[OrderNotification] Skipped ({Kind}) — MAALCA_WEB_URL/INTERNAL_NOTIFICATIONS_SECRET not set", kind);
            return;
        }

        try
        {
            var items = JsonArrayField.Parse<OrderItemDto>(order.ItemsJson);
            var payload = new
            {
                kind,
                orderId = order.Id.ToString(),
                businessName = order.Affiliate?.Name ?? "",
                slug = order.Affiliate?.Slug ?? "",
                customerEmail = order.CustomerEmail,
                customerName = order.CustomerName,
                items = items.Select(i => new { name = i.Name, price = i.Price, qty = i.Qty }),
                total = order.Total,
                currency = order.Currency,
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/internal/notifications/order")
            {
                Content = content,
            };
            request.Headers.Add("X-Internal-Secret", secret);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[OrderNotification] {Kind} notification failed ({Status}): {Body}", kind, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OrderNotification] {Kind} notification threw", kind);
        }
    }
}
