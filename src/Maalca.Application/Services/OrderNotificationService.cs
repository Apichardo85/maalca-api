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
    private readonly IOwnerNotificationService _owner;

    public OrderNotificationService(IHttpClientFactory httpClientFactory, ILogger<OrderNotificationService> logger, IOwnerNotificationService owner)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _owner = owner;
    }

    private Task NotifyOwnerAsync(Order order)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var who = string.IsNullOrWhiteSpace(order.CustomerName) ? null : order.CustomerName.Trim();
        var total = $"{order.Total.ToString("0.00", inv)} {order.Currency}";
        var partsEs = new List<string>();
        var partsEn = new List<string>();
        if (who is not null) { partsEs.Add(who); partsEn.Add(who); }
        partsEs.Add(total); partsEn.Add(total);
        if (!string.IsNullOrWhiteSpace(order.TableNumber)) { partsEs.Add($"Mesa {order.TableNumber}"); partsEn.Add($"Table {order.TableNumber}"); }
        if (order.ScheduledFor is { } day)
        {
            var d = day.ToString("yyyy-MM-dd", inv);
            partsEs.Add($"programado para {d}"); partsEn.Add($"scheduled for {d}");
        }
        var scheduled = order.ScheduledFor is not null;
        return _owner.NotifyAsync(
            order.AffiliateId, "order",
            scheduled ? "Nuevo pedido programado" : "Nuevo pedido",
            string.Join(" · ", partsEs),
            scheduled ? "New scheduled order" : "New order",
            string.Join(" · ", partsEn),
            "orders", order.Id);
    }

    public Task NotifyPayAtTableRequestedAsync(Order order)
    {
        // Sin esto el personal solo se enteraba al aceptar el pedido (que es cuando pasa a Paid): un pedido
        // de mesa por aceptar no avisaba justo cuando más urgente es.
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var who = string.IsNullOrWhiteSpace(order.CustomerName) ? null : order.CustomerName.Trim();
        var total = $"{order.Total.ToString("0.00", inv)} {order.Currency}";
        var table = order.TableNumber;
        var pickup = order.PaymentMethod == "PayAtPickup";
        var phone = string.IsNullOrWhiteSpace(order.CustomerPhone) ? null : order.CustomerPhone.Trim();
        var es = string.Join(" · ", new[] { who, total, string.IsNullOrWhiteSpace(table) ? null : $"Mesa {table}", pickup ? phone : null }.Where(x => x is not null));
        var en = string.Join(" · ", new[] { who, total, string.IsNullOrWhiteSpace(table) ? null : $"Table {table}", pickup ? phone : null }.Where(x => x is not null));
        return _owner.NotifyAsync(
            order.AffiliateId, "order",
            pickup ? "Pedido para recoger por aceptar" : "Pedido de mesa por aceptar", es,
            pickup ? "Pickup order to accept" : "Table order to accept", en,
            "orders", order.Id);
    }

    public Task NotifyOrderConfirmedAsync(Order order) => SendAsync(order, "confirmed");

    public Task NotifyOrderFulfilledAsync(Order order) => SendAsync(order, "fulfilled");

    private async Task SendAsync(Order order, string kind)
    {
        // Aviso al dueño (badge + push): solo cuando entra un pedido (pagado / confirmado), no al cumplirlo.
        // Va ANTES de los early-returns del correo: el pedido avisa aunque el cliente no haya dado correo.
        // Un pedido de mesa "pagar al mesero" ya avisó al llegar (NotifyPayAtTableRequestedAsync): al aceptarlo no se repite.
        if (kind == "confirmed" && order.PaymentMethod is not ("PayAtTable" or "PayAtPickup"))
            await NotifyOwnerAsync(order);

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
                logoUrl = string.IsNullOrWhiteSpace(order.Affiliate?.LogoUrl) ? order.Affiliate?.Logo : order.Affiliate?.LogoUrl,
                brandColor = order.Affiliate?.PrimaryColor,
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
