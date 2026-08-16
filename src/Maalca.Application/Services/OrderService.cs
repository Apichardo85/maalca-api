using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace Maalca.Application.Services;

/// <summary>
/// Pedidos reales del storefront público. Cobro (cuando el afiliado tiene Connect activo) vía
/// Checkout Session en modo "payment", ejecutada CON el header Stripe-Account de la cuenta
/// conectada del afiliado (RequestOptions.StripeAccount) — eso la convierte en un direct
/// charge: el dinero entra directo a la cuenta del afiliado, MaalCa nunca la toca.
///
/// Dos caminos confirman el pago, y ambos convergen en MarkPaidAsync: (1) ConfirmCheckoutAsync,
/// síncrono, cuando el cliente vuelve del Checkout — feedback inmediato en el navegador; (2)
/// ConfirmFromWebhookAsync, vía el evento checkout.session.completed suscrito en el webhook de
/// Connect (StripeConnectService) — la red de seguridad si el cliente cierra la pestaña antes
/// de volver. El webhook es la fuente de verdad real; el síncrono es solo UX más rápida.
/// </summary>
public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly IOrderNotificationService _notifications;
    private readonly IOrderRealtimeNotifier _realtime;

    public OrderService(AppDbContext db, IOrderNotificationService notifications, IOrderRealtimeNotifier realtime)
    {
        _db = db;
        _notifications = notifications;
        _realtime = realtime;
    }

    public async Task<CreateOrderResponseDto?> CreateOrderAsync(string affiliateSlug, CreateOrderRequest request)
    {
        var affiliate = await _db.Affiliates.FirstOrDefaultAsync(a => a.Slug == affiliateSlug && a.Published);
        if (affiliate is null) return null;
        if (request.Items.Count == 0) throw new ArgumentException("Order must have at least one item.");

        var order = new Order
        {
            AffiliateId = affiliate.Id,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            CustomerEmail = request.CustomerEmail,
            Notes = request.Notes,
            ItemsJson = JsonArrayField.Serialize(request.Items),
            Subtotal = request.Subtotal,
            Tax = request.Tax,
            Total = request.Total,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.ToUpperInvariant(),
            Status = OrderStatus.Pending,
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Sin Connect activo: el pedido queda guardado igual (visible en el panel admin), pero
        // no hay cobro online — el storefront cae al botón de WhatsApp de siempre.
        if (!affiliate.StripeConnectChargesEnabled || string.IsNullOrEmpty(affiliate.StripeConnectAccountId))
            return new CreateOrderResponseDto(order.Id, CheckoutUrl: null);

        if (string.IsNullOrEmpty(request.SuccessUrl) || string.IsNullOrEmpty(request.CancelUrl))
            return new CreateOrderResponseDto(order.Id, CheckoutUrl: null);

        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        var requestOptions = new RequestOptions { StripeAccount = affiliate.StripeConnectAccountId };

        var lineItems = request.Items.Select(i => new SessionLineItemOptions
        {
            Quantity = i.Qty,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = order.Currency.ToLowerInvariant(),
                UnitAmount = (long)Math.Round(i.Price * 100),
                ProductData = new SessionLineItemPriceDataProductDataOptions { Name = i.Name },
            },
        }).ToList();

        if (request.Tax > 0)
        {
            lineItems.Add(new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = order.Currency.ToLowerInvariant(),
                    UnitAmount = (long)Math.Round(request.Tax * 100),
                    ProductData = new SessionLineItemPriceDataProductDataOptions { Name = "Tax" },
                },
            });
        }

        var session = await new SessionService().CreateAsync(new SessionCreateOptions
        {
            Mode = "payment",
            LineItems = lineItems,
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            ClientReferenceId = order.Id.ToString(),
            CustomerEmail = string.IsNullOrEmpty(request.CustomerEmail) ? null : request.CustomerEmail,
        }, requestOptions);

        order.StripeCheckoutSessionId = session.Id;
        await _db.SaveChangesAsync();

        return new CreateOrderResponseDto(order.Id, session.Url);
    }

    public async Task<OrderDto?> CreatePosOrderAsync(Guid affiliateId, CreatePosOrderRequest request)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId);
        if (affiliate is null) return null;
        if (request.Items.Count == 0) throw new ArgumentException("Order must have at least one item.");

        var order = new Order
        {
            AffiliateId = affiliateId,
            CustomerName = request.CustomerName,
            Notes = request.Notes,
            ItemsJson = JsonArrayField.Serialize(request.Items),
            Subtotal = request.Subtotal,
            Tax = request.Tax,
            Total = request.Total,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.ToUpperInvariant(),
            // A diferencia de CreateOrderAsync (Pending -> espera Stripe Checkout), el POS entra
            // directo Paid: el cobro presencial (efectivo/tarjeta externa) ya pasó en el
            // mostrador antes de tocar "Cobrar" aquí.
            Status = OrderStatus.Paid,
            Channel = "POS",
            PaymentMethod = request.PaymentMethod,
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await _notifications.NotifyOrderConfirmedAsync(order);
        var dto = ToDto(order);
        // Mismo canal realtime que un pedido online recién pagado — aparece igual de "Nuevo"
        // en el Kitchen Display, sin que la cocina tenga que saber de dónde vino.
        await _realtime.NotifyOrderUpdatedAsync(affiliateId, dto);
        return dto;
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(Guid affiliateId)
    {
        var orders = await _db.Orders
            .Where(o => o.AffiliateId == affiliateId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return orders.Select(ToDto).ToList();
    }

    public async Task<OrderDto?> UpdateStatusAsync(Guid affiliateId, Guid orderId, string status)
    {
        var order = await _db.Orders.Include(o => o.Affiliate)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.AffiliateId == affiliateId);
        if (order is null) return null;
        if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
            throw new ArgumentException($"Invalid status '{status}'.");

        order.Status = parsed;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (parsed == OrderStatus.Fulfilled)
            await _notifications.NotifyOrderFulfilledAsync(order);

        var dto = ToDto(order);
        await _realtime.NotifyOrderUpdatedAsync(affiliateId, dto);
        return dto;
    }

    public async Task<OrderDto?> ConfirmCheckoutAsync(Guid orderId, string checkoutSessionId)
    {
        var order = await _db.Orders.Include(o => o.Affiliate).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null || order.Affiliate is null) return null;
        if (order.StripeCheckoutSessionId != checkoutSessionId) return null; // no confiar en el id sin validarlo contra el pedido

        if (order.Status == OrderStatus.Pending)
        {
            StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
            var requestOptions = new RequestOptions { StripeAccount = order.Affiliate.StripeConnectAccountId };
            var session = await new SessionService().GetAsync(checkoutSessionId, requestOptions: requestOptions);

            if (session.PaymentStatus == "paid")
                await MarkPaidAsync(order, session.PaymentIntentId);
        }

        return ToDto(order);
    }

    public async Task ConfirmFromWebhookAsync(string checkoutSessionId, string? paymentIntentId)
    {
        // Busca por StripeCheckoutSessionId, no por Id de pedido — el webhook solo trae el id de
        // la Session de Stripe, no el nuestro (nunca lo mandamos en la URL del webhook).
        var order = await _db.Orders.Include(o => o.Affiliate)
            .FirstOrDefaultAsync(o => o.StripeCheckoutSessionId == checkoutSessionId);
        if (order is null || order.Status != OrderStatus.Pending) return; // ya confirmado por el camino síncrono, o no es nuestro

        await MarkPaidAsync(order, paymentIntentId);
    }

    private async Task MarkPaidAsync(Order order, string? paymentIntentId)
    {
        order.Status = OrderStatus.Paid;
        order.StripePaymentIntentId = paymentIntentId;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _notifications.NotifyOrderConfirmedAsync(order);
        // Pedido recién pagado — aparece como "Nuevo" en el Kitchen Display.
        await _realtime.NotifyOrderUpdatedAsync(order.AffiliateId, ToDto(order));
    }

    private static OrderDto ToDto(Order o) => new(
        o.Id,
        o.CustomerName,
        o.CustomerPhone,
        o.CustomerEmail,
        o.Notes,
        JsonArrayField.Parse<OrderItemDto>(o.ItemsJson),
        o.Subtotal,
        o.Tax,
        o.Total,
        o.Currency,
        o.Status.ToString(),
        o.CreatedAt,
        o.Channel,
        o.PaymentMethod
    );
}
