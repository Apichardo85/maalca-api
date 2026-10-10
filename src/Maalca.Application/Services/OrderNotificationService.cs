using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
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
    private readonly AppDbContext _db;

    public OrderNotificationService(IHttpClientFactory httpClientFactory, ILogger<OrderNotificationService> logger, IOwnerNotificationService owner, AppDbContext db)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _owner = owner;
        _db = db;
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

    public Task NotifyOrderReceivedAsync(Order order) => SendAsync(order, "received");

    public async Task NotifyCustomerPushAsync(Order order, string kind)
    {
        try
        {
            if (string.IsNullOrEmpty(order.TrackingToken)) return;
            var baseUrl = Environment.GetEnvironmentVariable("MAALCA_WEB_URL");
            var secret = Environment.GetEnvironmentVariable("INTERNAL_NOTIFICATIONS_SECRET");
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret)) return;

            var subs = await _db.Set<OrderPushSubscription>().Where(s => s.OrderId == order.Id).ToListAsync();
            if (subs.Count == 0) return;

            var name = order.Affiliate?.Name ?? (await _db.Affiliates.Where(a => a.Id == order.AffiliateId).Select(a => a.Name).FirstOrDefaultAsync()) ?? "";
            int? mins = order.EstimatedReadyAt is { } eta ? Math.Max(1, (int)Math.Ceiling((eta - DateTime.UtcNow).TotalMinutes)) : null;
            var unpaid = order.PaymentMethod is "PayAtPickup" or "PayAtTable" && order.CollectedAt is null;
            var total = $"{order.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} {order.Currency}";

            string es, bodyEs, en, bodyEn;
            switch (kind)
            {
                case "accepted":
                    es = "Pedido aceptado ✅"; bodyEs = mins is null ? name : $"Listo en ~{mins} min · {name}";
                    en = "Order accepted ✅"; bodyEn = mins is null ? name : $"Ready in ~{mins} min · {name}";
                    break;
                case "delayed":
                    es = "Tu pedido se retrasó un poco"; bodyEs = mins is null ? name : $"Ahora estará listo en ~{mins} min · {name}";
                    en = "Your order is running a little late"; bodyEn = mins is null ? name : $"Now ready in ~{mins} min · {name}";
                    break;
                case "ready":
                    es = "¡Tu pedido está listo! 🛍️"; bodyEs = unpaid ? $"Pasa a recogerlo · pagas {total} · {name}" : $"Pasa a recogerlo · {name}";
                    en = "Your order is ready! 🛍️"; bodyEn = unpaid ? $"Come pick it up · you pay {total} · {name}" : $"Come pick it up · {name}";
                    break;
                case "canceled":
                    es = "Pedido cancelado"; bodyEs = $"{name} canceló tu pedido. Contáctalos si tienes dudas.";
                    en = "Order canceled"; bodyEn = $"{name} canceled your order. Contact them if you have questions.";
                    break;
                default:
                    return;
            }

            var payload = new
            {
                subscriptions = subs.Select(s => new { endpoint = s.Endpoint, p256dh = s.P256dh, auth = s.Auth, lang = s.Lang }),
                message = new
                {
                    type = "order-customer", title = es, body = bodyEs, titleEn = en, bodyEn,
                    slug = (string?)null, url = (string?)null, path = $"/t/{order.TrackingToken}",
                },
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/internal/notifications/push") { Content = content };
            request.Headers.Add("X-Internal-Secret", secret);
            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode) { _logger.LogWarning("[OrderNotification] Customer push failed ({Status})", response.StatusCode); return; }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("expired", out var expiredEl) && expiredEl.ValueKind == JsonValueKind.Array)
            {
                var expired = expiredEl.EnumerateArray().Select(e => e.GetString()).Where(e => !string.IsNullOrEmpty(e)).ToList();
                if (expired.Count > 0)
                {
                    _db.Set<OrderPushSubscription>().RemoveRange(subs.Where(s => expired.Contains(s.Endpoint)));
                    await _db.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OrderNotification] Customer push threw ({Kind})", kind);
        }
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

        // Un solo correo por pedido, con el enlace de seguimiento: pagar al recoger/mesero avisa al recibirlo ("received");
        // pagado online avisa al pagar ("confirmed"). Aceptar y entregar ya no mandan correo (el enlace muestra el estado).
        var payLater = order.PaymentMethod is "PayAtPickup" or "PayAtTable";
        if (kind == "fulfilled" && order.TrackingToken is not null) return;
        if (kind == "confirmed" && payLater) return;
        if (kind == "received" && !payLater) return;

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
                trackUrl = order.TrackingToken is null ? null : $"{baseUrl.TrimEnd('/')}/t/{order.TrackingToken}",
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
                language = order.Affiliate?.Language ?? "es",
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
