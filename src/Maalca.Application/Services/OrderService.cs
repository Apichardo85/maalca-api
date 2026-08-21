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
            Tip = request.Tip,
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
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = string.IsNullOrWhiteSpace(i.Notes) ? i.Name : $"{i.Name} ({i.Notes})",
                },
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

        if (request.Tip > 0)
        {
            lineItems.Add(new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = order.Currency.ToLowerInvariant(),
                    UnitAmount = (long)Math.Round(request.Tip * 100),
                    ProductData = new SessionLineItemPriceDataProductDataOptions { Name = "Tip" },
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
            Tip = request.Tip,
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
        await DecrementStockAsync(order);
        await _db.SaveChangesAsync();

        await _notifications.NotifyOrderConfirmedAsync(order);
        var dto = ToDto(order);
        // Mismo canal realtime que un pedido online recién pagado — aparece igual de "Nuevo"
        // en el Kitchen Display, sin que la cocina tenga que saber de dónde vino.
        await _realtime.NotifyOrderUpdatedAsync(affiliateId, dto);
        return dto;
    }

    public async Task<CreateOrderResponseDto?> CreatePosCheckoutAsync(Guid affiliateId, CreatePosCheckoutRequest request)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId);
        if (affiliate is null) return null;
        if (request.Items.Count == 0) throw new ArgumentException("Order must have at least one item.");

        // A diferencia del storefront público (que cae calladito a WhatsApp si no hay Connect),
        // acá el staff está parado frente al cliente esperando cobrar — mejor fallar visible con
        // un mensaje accionable que devolver un CheckoutUrl null sin explicación.
        if (!affiliate.StripeConnectChargesEnabled || string.IsNullOrEmpty(affiliate.StripeConnectAccountId))
            throw new InvalidOperationException("Conecta Stripe en Configuración antes de cobrar con QR.");

        var order = new Order
        {
            AffiliateId = affiliateId,
            CustomerName = request.CustomerName,
            Notes = request.Notes,
            ItemsJson = JsonArrayField.Serialize(request.Items),
            Subtotal = request.Subtotal,
            Tax = request.Tax,
            Tip = request.Tip,
            Total = request.Total,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.ToUpperInvariant(),
            Status = OrderStatus.Pending,
            Channel = "POS",
            PaymentMethod = "Card",
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        var requestOptions = new RequestOptions { StripeAccount = affiliate.StripeConnectAccountId };

        var lineItems = request.Items.Select(i => new SessionLineItemOptions
        {
            Quantity = i.Qty,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = order.Currency.ToLowerInvariant(),
                UnitAmount = (long)Math.Round(i.Price * 100),
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = string.IsNullOrWhiteSpace(i.Notes) ? i.Name : $"{i.Name} ({i.Notes})",
                },
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

        if (request.Tip > 0)
        {
            lineItems.Add(new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = order.Currency.ToLowerInvariant(),
                    UnitAmount = (long)Math.Round(request.Tip * 100),
                    ProductData = new SessionLineItemPriceDataProductDataOptions { Name = "Tip" },
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
        }, requestOptions);

        order.StripeCheckoutSessionId = session.Id;
        await _db.SaveChangesAsync();

        return new CreateOrderResponseDto(order.Id, session.Url);
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
        await DecrementStockAsync(order);
        await _db.SaveChangesAsync();

        await _notifications.NotifyOrderConfirmedAsync(order);
        // Pedido recién pagado — aparece como "Nuevo" en el Kitchen Display.
        await _realtime.NotifyOrderUpdatedAsync(order.AffiliateId, ToDto(order));
    }

    /// <summary>
    /// Descuenta stock real de InventoryItem cuando un pedido pasa a Paid — antes de esto, un
    /// negocio de Retail podía vender el mismo último producto N veces sin que el sistema se
    /// enterara (ni Product.Stock ni InventoryItem.Quantity se tocaban en ningún camino de
    /// creación de Order). Solo afecta ItemId que resuelvan a un InventoryItem real del mismo
    /// afiliado (Retail) — Product (Restaurant) y Service (Barber/Service) no llevan control de
    /// stock por diseño, así que sus items simplemente no matchean y no pasa nada. Nunca deja
    /// Quantity negativo (clamp a 0) para no mostrar stock "negativo" en la UI.
    /// </summary>
    private async Task DecrementStockAsync(Order order)
    {
        var items = JsonArrayField.Parse<OrderItemDto>(order.ItemsJson);
        if (items.Count == 0) return;

        var itemIds = items
            .Select(i => Guid.TryParse(i.ItemId, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .Distinct()
            .ToList();
        if (itemIds.Count == 0) return;

        // Camino directo (Retail): el Catálogo ES InventoryItem, misma fila/Id (task #175).
        var inventoryItems = await _db.InventoryItems
            .Where(inv => inv.AffiliateId == order.AffiliateId && itemIds.Contains(inv.Id))
            .ToListAsync();

        foreach (var item in items)
        {
            if (!Guid.TryParse(item.ItemId, out var itemId)) continue;
            var inv = inventoryItems.FirstOrDefault(i => i.Id == itemId);
            if (inv is null) continue;

            inv.Quantity = Math.Max(0, inv.Quantity - item.Qty);
            _db.InventoryMovements.Add(new InventoryMovement
            {
                InventoryItemId = inv.Id,
                Type = "out",
                Quantity = item.Qty,
                Notes = $"Venta — Pedido #{order.Id.ToString()[..8]}",
            });
        }

        // Camino de receta (Restaurante): itemId es un Product (plato), no un InventoryItem
        // directo — se resuelve vía ProductIngredient a los ingredientes reales y se descuenta
        // Quantity(receta) x cantidad vendida. Sin esto un plato nunca tocaba ningún ingrediente
        // (task #291/#292 — la causa concreta de "el módulo de inventario es una mierda" para
        // Restaurante, a diferencia de Retail arriba).
        var recipeLines = await _db.ProductIngredients
            .Where(pi => itemIds.Contains(pi.ProductId))
            .ToListAsync();
        if (recipeLines.Count == 0) return;

        var ingredientIds = recipeLines.Select(pi => pi.InventoryItemId).Distinct().ToList();
        var ingredientItems = await _db.InventoryItems
            .Where(inv => inv.AffiliateId == order.AffiliateId && ingredientIds.Contains(inv.Id))
            .ToListAsync();

        foreach (var item in items)
        {
            if (!Guid.TryParse(item.ItemId, out var productId)) continue;
            foreach (var line in recipeLines.Where(pi => pi.ProductId == productId))
            {
                var inv = ingredientItems.FirstOrDefault(i => i.Id == line.InventoryItemId);
                if (inv is null) continue;

                // InventoryItem.Quantity es int; la receta es decimal (ej. 0.5 kg por plato) —
                // redondeamos hacia arriba para no sub-descontar el ingrediente real.
                var consumed = (int)Math.Ceiling(line.Quantity * item.Qty);
                if (consumed <= 0) continue;

                inv.Quantity = Math.Max(0, inv.Quantity - consumed);
                _db.InventoryMovements.Add(new InventoryMovement
                {
                    InventoryItemId = inv.Id,
                    Type = "out",
                    Quantity = consumed,
                    Notes = $"Venta (receta) — Pedido #{order.Id.ToString()[..8]}",
                });
            }
        }
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
        o.PaymentMethod,
        o.Tip
    );
}
