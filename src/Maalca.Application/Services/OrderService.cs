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

        // El navegador NO es fuente de verdad de precios: nombre y precio salen del catálogo del
        // afiliado, y subtotal/total se recalculan aquí. Sin esto, un cliente podía mandar
        // price=0.01 y Stripe cobraba eso (UnitAmount se arma con estos valores).
        // Cerrado ahora => el pedido solo se acepta programado para la próxima apertura, y el menú
        // que se valida es el de esa fecha (no el de hoy).
        var scheduledFor = ResolveSchedule(affiliate, request);
        var priced = await RepriceItemsAsync(affiliate.Id, request.Items, request.Tax, request.Tip, scheduledFor);

        var tableNumber = await ValidateTableAsync(affiliate, request);
        if (request.PayAtPickup) await ValidatePickupAsync(affiliate, request, tableNumber);
        var customer = await LinkCustomerAsync(affiliate.Id, request.CustomerName, request.CustomerPhone, request.CustomerEmail);

        var order = new Order
        {
            AffiliateId = affiliate.Id,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            CustomerEmail = request.CustomerEmail,
            Notes = request.Notes,
            ItemsJson = JsonArrayField.Serialize(priced.Items),
            Subtotal = priced.Subtotal,
            Tax = priced.Tax,
            Tip = priced.Tip,
            Total = priced.Total,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.ToUpperInvariant(),
            Status = OrderStatus.Pending,
            TableNumber = tableNumber,
            CustomerId = customer?.Id,
            Channel = tableNumber is null ? "Online" : "Table",
            PaymentMethod = tableNumber is not null && request.PayAtTable ? PayAtTableMethod
                : tableNumber is null && request.PayAtPickup ? PayAtPickupMethod : null,
            ScheduledFor = scheduledFor,
            TrackingToken = NewTrackingToken(),
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Pagar al mesero: sin Stripe. Queda Pending y le aparece al personal en el panel
        // (realtime) para que lo acepte; no entra a cocina hasta entonces.
        if (order.PaymentMethod == PayAtTableMethod || order.PaymentMethod == PayAtPickupMethod)
        {
            await _notifications.NotifyPayAtTableRequestedAsync(order);
            order.Affiliate = affiliate;
            await _notifications.NotifyOrderReceivedAsync(order);
            await _realtime.NotifyOrderUpdatedAsync(affiliate.Id, ToDto(order));
            return new CreateOrderResponseDto(order.Id, CheckoutUrl: null, TrackingToken: order.TrackingToken);
        }

        // Sin Connect activo: el pedido queda guardado igual (visible en el panel admin), pero
        // no hay cobro online — el storefront cae al botón de WhatsApp de siempre.
        if (!affiliate.StripeConnectChargesEnabled || string.IsNullOrEmpty(affiliate.StripeConnectAccountId))
            return new CreateOrderResponseDto(order.Id, CheckoutUrl: null, TrackingToken: order.TrackingToken);

        if (string.IsNullOrEmpty(request.SuccessUrl) || string.IsNullOrEmpty(request.CancelUrl))
            return new CreateOrderResponseDto(order.Id, CheckoutUrl: null, TrackingToken: order.TrackingToken);

        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        var requestOptions = new RequestOptions { StripeAccount = affiliate.StripeConnectAccountId };

        var lineItems = priced.Items.Select(i => new SessionLineItemOptions
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

        if (priced.Tax > 0)
        {
            lineItems.Add(new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = order.Currency.ToLowerInvariant(),
                    UnitAmount = (long)Math.Round(priced.Tax * 100),
                    ProductData = new SessionLineItemPriceDataProductDataOptions { Name = "Tax" },
                },
            });
        }

        if (priced.Tip > 0)
        {
            lineItems.Add(new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = order.Currency.ToLowerInvariant(),
                    UnitAmount = (long)Math.Round(priced.Tip * 100),
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
            // Pide el teléfono en el Checkout: junto con el nombre/correo que Stripe ya captura,
            // completa el cliente cuando no lo dio antes de pagar (ver MarkPaidAsync).
            PhoneNumberCollection = new SessionPhoneNumberCollectionOptions { Enabled = true },
        }, requestOptions);

        order.StripeCheckoutSessionId = session.Id;
        await _db.SaveChangesAsync();

        return new CreateOrderResponseDto(order.Id, session.Url);
    }

    private const int MaxQtyPerLine = 99;

    // Todavía no hay tasa de impuesto configurable por afiliado (el storefront manda taxRate=0),
    // así que el servidor no puede recalcular el impuesto: solo lo acota. Cuando exista
    // Affiliate.TaxRate, este tope se reemplaza por el cálculo real (subtotal * tasa).
    private const decimal MaxTaxFractionOfSubtotal = 0.25m;

    private sealed record PricedOrder(
        IReadOnlyList<OrderItemDto> Items, decimal Subtotal, decimal Tax, decimal Tip, decimal Total);

    /// <summary>
    /// Reconstruye las líneas del pedido con nombre y precio del catálogo real del afiliado
    /// (Product, Service o InventoryItem publicado y activo — mismo criterio que
    /// PublicCatalogService), ignorando los precios que mandó el navegador. Rechaza artículos
    /// ajenos, ocultos, inactivos o inexistentes, cantidades fuera de rango y montos negativos.
    /// </summary>
    private async Task<PricedOrder> RepriceItemsAsync(
        Guid affiliateId, IReadOnlyList<OrderItemDto> requested, decimal clientTax, decimal clientTip,
        DateOnly? scheduledFor = null)
    {
        if (requested.Any(i => i.Qty < 1 || i.Qty > MaxQtyPerLine))
            throw new ArgumentException($"Cantidad inválida (debe ser entre 1 y {MaxQtyPerLine}).");
        if (clientTax < 0 || clientTip < 0)
            throw new ArgumentException("Impuesto y propina no pueden ser negativos.");

        var ids = new List<Guid>(requested.Count);
        foreach (var line in requested)
        {
            if (!Guid.TryParse(line.ItemId, out var id))
                throw new ArgumentException("Artículo inválido.");
            ids.Add(id);
        }
        var distinctIds = ids.Distinct().ToList();

        var catalog = new Dictionary<Guid, (string Name, decimal Price)>();

        var products = await _db.Products
            .Where(p => p.AffiliateId == affiliateId && distinctIds.Contains(p.Id)
                        && p.IsPubliclyVisible && p.Status == "Active")
            .Select(p => new { p.Id, p.Name, p.Price, p.WeekDays })
            .ToListAsync();
        foreach (var p in products) catalog[p.Id] = (p.Name, p.Price);

        // Disponibilidad por día de la semana (Product.WeekDays, tokens monday..sunday): un plato
        // del sábado no se puede pedir un martes aunque alguien tenga la página abierta desde
        // el sábado. Se evalúa en la zona horaria del NEGOCIO. Sin WeekDays = todos los días.
        // Periods (desayuno/almuerzo/cena) NO se hace cumplir acá a propósito: el rango por
        // defecto de cada período no coincide con el horario real de cada negocio.
        var restricted = products.Where(p => !string.IsNullOrWhiteSpace(p.WeekDays)).ToList();
        if (restricted.Count > 0)
        {
            // Programado: el día de la fecha elegida. Inmediato: hoy en la zona del negocio.
            DayOfWeek dow;
            if (scheduledFor is { } sf)
            {
                dow = sf.DayOfWeek;
            }
            else
            {
                var ianaTz = await _db.Affiliates.Where(a => a.Id == affiliateId).Select(a => a.Timezone).FirstOrDefaultAsync();
                var tz = TimeZoneInfo.Utc;
                if (!string.IsNullOrWhiteSpace(ianaTz))
                {
                    try { tz = TimeZoneInfo.FindSystemTimeZoneById(ianaTz); }
                    catch (Exception) { /* zona inválida: cae a UTC */ }
                }
                dow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).DayOfWeek;
            }
            var day = dow.ToString().ToLowerInvariant();
            foreach (var p in restricted)
            {
                var days = TokenList.Parse(p.WeekDays);
                if (!days.Contains(day, StringComparer.OrdinalIgnoreCase))
                    throw new ArgumentException(scheduledFor is null
                        ? $"\"{p.Name}\" no está disponible hoy."
                        : $"\"{p.Name}\" no está disponible el día para el que programaste el pedido.");
            }
        }

        var services = await _db.Services
            .Where(s => s.AffiliateId == affiliateId && distinctIds.Contains(s.Id)
                        && s.IsPubliclyVisible && s.Status == "Active")
            .Select(s => new { s.Id, s.Name, s.Price })
            .ToListAsync();
        foreach (var s in services) catalog.TryAdd(s.Id, (s.Name, s.Price));

        var inventory = await _db.InventoryItems
            .Where(i => i.AffiliateId == affiliateId && distinctIds.Contains(i.Id)
                        && i.IsPubliclyVisible && i.Status == "Active")
            .Select(i => new { i.Id, i.Name, Price = i.UnitPrice })
            .ToListAsync();
        foreach (var i in inventory) catalog.TryAdd(i.Id, (i.Name, i.Price));

        var lines = new List<OrderItemDto>(requested.Count);
        decimal subtotal = 0;
        for (var idx = 0; idx < requested.Count; idx++)
        {
            var req = requested[idx];
            if (!catalog.TryGetValue(ids[idx], out var entry))
                throw new ArgumentException("Uno de los artículos ya no está disponible.");
            lines.Add(new OrderItemDto(req.ItemId, entry.Name, entry.Price, req.Qty, req.Notes));
            subtotal += entry.Price * req.Qty;
        }

        subtotal = Math.Round(subtotal, 2);
        var tax = Math.Round(clientTax, 2);
        var tip = Math.Round(clientTip, 2);
        if (tax > Math.Round(subtotal * MaxTaxFractionOfSubtotal, 2))
            throw new ArgumentException("Impuesto inválido para este pedido.");

        return new PricedOrder(lines, subtotal, tax, tip, subtotal + tax + tip);
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

    public async Task<OrderDto?> UpdateStatusAsync(Guid affiliateId, Guid orderId, string status, int? estimatedMinutes = null)
    {
        var order = await _db.Orders.Include(o => o.Affiliate)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.AffiliateId == affiliateId);
        if (order is null) return null;
        if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
            throw new ArgumentException($"Invalid status '{status}'.");

        // Aceptar / "Marcar pagado" desde el panel (Pending -> Paid) pasa por el mismo camino que el pago con
        // Stripe: descuenta inventario, enlaza al cliente y avisa. Antes solo cambiaba el estado y el stock
        // nunca se descontaba. PaymentMethod queda como PayAtTable (mesa) o Manual (cobro fuera de Stripe).
        if (parsed == OrderStatus.Paid && order.Status == OrderStatus.Pending)
        {
            order.PaymentMethod ??= "Manual";
            if (estimatedMinutes is > 0 and <= 240) order.EstimatedReadyAt = DateTime.UtcNow.AddMinutes(estimatedMinutes.Value);
            await MarkPaidAsync(order, null);
            await _notifications.NotifyCustomerPushAsync(order, "accepted");
            return ToDto(order);
        }

        var previousStatus = order.Status;
        order.Status = parsed;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (parsed == OrderStatus.Fulfilled)
        {
            // Un pedido entregado cuenta como visita del cliente (antes TotalVisits solo subía con citas/filas/reservas).
            if (previousStatus != OrderStatus.Fulfilled && order.CustomerId is { } customerId)
            {
                var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
                if (customer is not null)
                {
                    customer.TotalVisits += 1;
                    customer.LastVisit = DateTime.UtcNow;
                    customer.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
            }
            await _notifications.NotifyOrderFulfilledAsync(order);
            await _notifications.NotifyCustomerPushAsync(order, "ready");
        }
        else if (parsed == OrderStatus.Canceled)
            await _notifications.NotifyCustomerPushAsync(order, "canceled");

        var dto = ToDto(order);
        await _realtime.NotifyOrderUpdatedAsync(affiliateId, dto);
        return dto;
    }

    public async Task<OrderDto?> SetEstimateAsync(Guid affiliateId, Guid orderId, int minutes)
    {
        if (minutes is < 1 or > 240) throw new ArgumentException("Minutes must be between 1 and 240.");
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.AffiliateId == affiliateId);
        if (order is null) return null;
        var previousEta = order.EstimatedReadyAt;
        order.EstimatedReadyAt = DateTime.UtcNow.AddMinutes(minutes);
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        if (previousEta is not null && order.EstimatedReadyAt > previousEta)
            await _notifications.NotifyCustomerPushAsync(order, "delayed");
        var dto = ToDto(order);
        await _realtime.NotifyOrderUpdatedAsync(affiliateId, dto);
        return dto;
    }

    public async Task<OrderTrackingDto?> GetTrackingAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64) return null;
        var order = await _db.Orders.Include(o => o.Affiliate).FirstOrDefaultAsync(o => o.TrackingToken == token);
        if (order?.Affiliate is null) return null;
        var a = order.Affiliate;
        // Pedido terminado hace más de 24 h: el enlace deja de mostrar el detalle.
        var expired = order.Status is OrderStatus.Fulfilled or OrderStatus.Canceled
            && (order.UpdatedAt ?? order.CreatedAt) < DateTime.UtcNow.AddHours(-24);
        var payAtVenue = order.PaymentMethod is PayAtPickupMethod or PayAtTableMethod;
        return new OrderTrackingDto(
            a.Name, a.Slug, string.IsNullOrWhiteSpace(a.LogoUrl) ? a.Logo : a.LogoUrl, a.PrimaryColor, a.Address, a.WhatsApp,
            order.Status.ToString(), payAtVenue, order.CollectedAt is not null,
            order.TableNumber, order.ScheduledFor?.ToString("yyyy-MM-dd"),
            order.CreatedAt, order.UpdatedAt ?? order.CreatedAt, order.EstimatedReadyAt,
            expired ? Array.Empty<OrderItemDto>() : JsonArrayField.Parse<OrderItemDto>(order.ItemsJson),
            order.Subtotal, order.Tax, order.Tip, order.Total, order.Currency, expired,
            CanPayOnline: CanPayOnline(order, a));
    }

    private static bool CanPayOnline(Order order, Affiliate a) =>
        a.StripeConnectChargesEnabled && !string.IsNullOrEmpty(a.StripeConnectAccountId)
        && order.CollectedAt is null && order.Status != OrderStatus.Canceled
        && (order.PaymentMethod is PayAtPickupMethod or PayAtTableMethod || order.Status == OrderStatus.Pending)
        && order.Status != OrderStatus.Fulfilled;

    public async Task<bool> SubscribeTrackingPushAsync(string token, PushSubscribeRequest request)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64) return false;
        if (string.IsNullOrWhiteSpace(request.Endpoint) || request.Endpoint.Length > 1000
            || string.IsNullOrWhiteSpace(request.P256dh) || request.P256dh.Length > 300
            || string.IsNullOrWhiteSpace(request.Auth) || request.Auth.Length > 100)
            throw new ArgumentException("Suscripción de push inválida.");
        var orderId = await _db.Orders.Where(o => o.TrackingToken == token).Select(o => (Guid?)o.Id).FirstOrDefaultAsync();
        if (orderId is null) return false;

        var existing = await _db.Set<OrderPushSubscription>().Where(s => s.OrderId == orderId.Value).ToListAsync();
        var same = existing.FirstOrDefault(s => s.Endpoint == request.Endpoint);
        if (same is not null)
        {
            same.P256dh = request.P256dh; same.Auth = request.Auth; same.Lang = request.Lang == "en" ? "en" : "es";
            same.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            if (existing.Count >= 3) throw new ArgumentException("Too many devices for this order.");
            _db.Set<OrderPushSubscription>().Add(new OrderPushSubscription
            {
                OrderId = orderId.Value, Endpoint = request.Endpoint, P256dh = request.P256dh, Auth = request.Auth,
                Lang = request.Lang == "en" ? "en" : "es",
            });
        }
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<TrackingPayResponseDto?> CreateOnlinePaymentAsync(string token, string successUrl, string cancelUrl)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64) return null;
        if (string.IsNullOrWhiteSpace(successUrl) || string.IsNullOrWhiteSpace(cancelUrl))
            throw new ArgumentException("Return URLs are required.");
        var order = await _db.Orders.Include(o => o.Affiliate).FirstOrDefaultAsync(o => o.TrackingToken == token);
        if (order?.Affiliate is null) return null;
        if (!CanPayOnline(order, order.Affiliate))
            throw new ArgumentException("Online payment is not available for this order.");

        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        var requestOptions = new RequestOptions { StripeAccount = order.Affiliate.StripeConnectAccountId };
        var currency = order.Currency.ToLowerInvariant();

        // Los precios ya fueron recalculados desde el catálogo al crear el pedido (RepriceItemsAsync): se reusan tal cual.
        var lineItems = JsonArrayField.Parse<OrderItemDto>(order.ItemsJson).Select(i => new SessionLineItemOptions
        {
            Quantity = i.Qty,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = currency,
                UnitAmount = (long)Math.Round(i.Price * 100),
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = string.IsNullOrWhiteSpace(i.Notes) ? i.Name : $"{i.Name} ({i.Notes})",
                },
            },
        }).ToList();
        foreach (var (name, amount) in new[] { ("Tax", order.Tax), ("Tip", order.Tip) })
        {
            if (amount <= 0) continue;
            lineItems.Add(new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = currency,
                    UnitAmount = (long)Math.Round(amount * 100),
                    ProductData = new SessionLineItemPriceDataProductDataOptions { Name = name },
                },
            });
        }

        var session = await new SessionService().CreateAsync(new SessionCreateOptions
        {
            Mode = "payment",
            LineItems = lineItems,
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            ClientReferenceId = order.Id.ToString(),
            CustomerEmail = string.IsNullOrEmpty(order.CustomerEmail) ? null : order.CustomerEmail,
        }, requestOptions);

        order.StripeCheckoutSessionId = session.Id;
        await _db.SaveChangesAsync();
        return new TrackingPayResponseDto(session.Url);
    }

    public async Task<OrderTrackingDto?> ConfirmOnlinePaymentAsync(string token, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64) return null;
        var id = await _db.Orders.Where(o => o.TrackingToken == token).Select(o => (Guid?)o.Id).FirstOrDefaultAsync();
        if (id is null) return null;
        await ConfirmCheckoutAsync(id.Value, sessionId);
        return await GetTrackingAsync(token);
    }

    /// <summary>Un pedido "pagar al recoger/mesero" que se paga online queda cobrado (CollectedMethod = Online).</summary>
    private async Task SettleOnlinePaymentAsync(Order order, string? paymentIntentId,
        string? name = null, string? email = null, string? phone = null)
    {
        var payAtVenue = order.PaymentMethod is PayAtPickupMethod or PayAtTableMethod;
        if (order.Status == OrderStatus.Pending)
            await MarkPaidAsync(order, paymentIntentId, name, email, phone);
        if (payAtVenue && order.CollectedAt is null)
        {
            order.CollectedAt = DateTime.UtcNow;
            order.CollectedMethod = "Online";
            order.StripePaymentIntentId ??= paymentIntentId;
            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _realtime.NotifyOrderUpdatedAsync(order.AffiliateId, ToDto(order));
        }
    }

    private static string NewTrackingToken() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public async Task<OrderDto?> CollectPaymentAsync(Guid affiliateId, Guid orderId, string method)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.AffiliateId == affiliateId);
        if (order is null) return null;
        var normalized = method?.Trim().ToLowerInvariant() switch
        {
            "cash" => "Cash", "card" => "Card", "other" => "Other",
            _ => throw new ArgumentException("Method must be Cash, Card or Other."),
        };
        if (order.PaymentMethod is not (PayAtPickupMethod or PayAtTableMethod))
            throw new ArgumentException("This order was already paid online.");
        if (order.Status is OrderStatus.Pending or OrderStatus.Canceled)
            throw new ArgumentException("Accept the order before collecting payment.");
        if (order.CollectedAt is not null) return ToDto(order);

        order.CollectedAt = DateTime.UtcNow;
        order.CollectedMethod = normalized;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var dto = ToDto(order);
        await _realtime.NotifyOrderUpdatedAsync(affiliateId, dto);
        return dto;
    }

    public async Task<OrderDto?> ConfirmCheckoutAsync(Guid orderId, string checkoutSessionId)
    {
        var order = await _db.Orders.Include(o => o.Affiliate).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null || order.Affiliate is null) return null;
        if (order.StripeCheckoutSessionId != checkoutSessionId) return null; // no confiar en el id sin validarlo contra el pedido

        var payAtVenue = order.PaymentMethod is PayAtPickupMethod or PayAtTableMethod;
        if (order.Status == OrderStatus.Pending || (payAtVenue && order.CollectedAt is null))
        {
            StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
            var requestOptions = new RequestOptions { StripeAccount = order.Affiliate.StripeConnectAccountId };
            var session = await new SessionService().GetAsync(checkoutSessionId, requestOptions: requestOptions);

            if (session.PaymentStatus == "paid")
                await SettleOnlinePaymentAsync(order, session.PaymentIntentId,
                    session.CustomerDetails?.Name, session.CustomerDetails?.Email, session.CustomerDetails?.Phone);
        }

        return ToDto(order);
    }

    public async Task ConfirmFromWebhookAsync(string checkoutSessionId, string? paymentIntentId,
        string? customerName = null, string? customerEmail = null, string? customerPhone = null)
    {
        // Busca por StripeCheckoutSessionId, no por Id de pedido — el webhook solo trae el id de
        // la Session de Stripe, no el nuestro (nunca lo mandamos en la URL del webhook).
        var order = await _db.Orders.Include(o => o.Affiliate)
            .FirstOrDefaultAsync(o => o.StripeCheckoutSessionId == checkoutSessionId);
        if (order is null) return;
        var payAtVenue = order.PaymentMethod is PayAtPickupMethod or PayAtTableMethod;
        if (order.Status != OrderStatus.Pending && !(payAtVenue && order.CollectedAt is null)) return; // ya confirmado por el camino síncrono

        await SettleOnlinePaymentAsync(order, paymentIntentId, customerName, customerEmail, customerPhone);
    }

    private async Task MarkPaidAsync(Order order, string? paymentIntentId,
        string? stripeName = null, string? stripeEmail = null, string? stripePhone = null)
    {
        order.Status = OrderStatus.Paid;
        order.StripePaymentIntentId = paymentIntentId;
        order.UpdatedAt = DateTime.UtcNow;

        // Lo que el cliente escribió antes de pagar manda; lo que Stripe capturó en el Checkout
        // (nombre de la tarjeta, correo, teléfono) solo completa lo que falte.
        if (string.IsNullOrWhiteSpace(order.CustomerName)) order.CustomerName = Clean(stripeName);
        if (string.IsNullOrWhiteSpace(order.CustomerEmail)) order.CustomerEmail = Clean(stripeEmail);
        if (string.IsNullOrWhiteSpace(order.CustomerPhone)) order.CustomerPhone = Clean(stripePhone);
        if (order.CustomerId is null)
        {
            var customer = await LinkCustomerAsync(order.AffiliateId, order.CustomerName, order.CustomerPhone, order.CustomerEmail);
            order.CustomerId = customer?.Id;
        }
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

                // InventoryItem.Quantity es decimal (antes int + Math.Ceiling) — se descuenta la
                // fracción exacta de la receta (ej. 0.5 kg por plato), sin redondear.
                var consumed = line.Quantity * item.Qty;
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

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    /// <summary>
    /// Enlaza el pedido con un Customer del negocio (mismo criterio que reservas y citas: dedup
    /// por teléfono; si no hay teléfono, por correo). Sin teléfono ni correo no se crea nada —
    /// no hay cómo reconocer al cliente la próxima vez.
    /// </summary>
    private async Task<Maalca.Domain.Entities.Customer?> LinkCustomerAsync(Guid affiliateId, string? name, string? phone, string? email)
    {
        phone = Clean(phone);
        email = Clean(email);
        name = Clean(name);
        if (phone is null && email is null) return null;

        var customer = await _db.Customers.FirstOrDefaultAsync(c =>
            c.AffiliateId == affiliateId &&
            ((phone != null && c.Phone == phone) || (email != null && c.Email == email)));
        if (customer is not null)
        {
            if (customer.Email is null && email is not null) customer.Email = email;
            if (customer.Phone is null && phone is not null) customer.Phone = phone;
            return customer;
        }

        customer = new Maalca.Domain.Entities.Customer
        {
            AffiliateId = affiliateId,
            Name = name ?? phone ?? email!,
            Phone = phone,
            Email = email,
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return customer;
    }

    private const string PayAtTableMethod = "PayAtTable";
    private const string PayAtPickupMethod = "PayAtPickup";
    private const int MaxPendingPickupPerContact = 2;

    /// <summary>Pedido para recoger pagando en el local: sin cobro online no hay otra barrera, así que
    /// exige contacto real y limita los pendientes por contacto (anti-spam).</summary>
    private async Task ValidatePickupAsync(Affiliate affiliate, CreateOrderRequest request, string? tableNumber)
    {
        if (tableNumber is not null) throw new ArgumentException("Pay-at-pickup cannot be combined with a table number.");
        var name = request.CustomerName?.Trim();
        var phone = request.CustomerPhone?.Trim();
        var email = request.CustomerEmail?.Trim();
        if (string.IsNullOrEmpty(name) || (string.IsNullOrEmpty(phone) && string.IsNullOrEmpty(email)))
            throw new ArgumentException("Pickup orders need a name and a phone or email.");
        var since = DateTime.UtcNow.AddHours(-3);
        var pending = await _db.Orders.CountAsync(o =>
            o.AffiliateId == affiliate.Id && o.PaymentMethod == PayAtPickupMethod && o.Status == OrderStatus.Pending &&
            o.CreatedAt >= since &&
            ((phone != null && phone != "" && o.CustomerPhone == phone) || (email != null && email != "" && o.CustomerEmail == email)));
        if (pending >= MaxPendingPickupPerContact)
            throw new ArgumentException("You already have pending orders. Please wait for the restaurant to confirm.");
    }
    private const int MaxTableLength = 20;
    private const int MaxPendingPayAtTablePerTable = 3;

    /// <summary>
    /// Valida el pedido de mesa y devuelve el número normalizado (o null si no es de mesa).
    /// Solo Restaurante. El tope de pedidos "pagar al mesero" pendientes por mesa evita que
    /// alguien fotografíe el QR y llene el panel de pedidos falsos sin pasar por Stripe.
    /// </summary>
    private async Task<string?> ValidateTableAsync(Affiliate affiliate, CreateOrderRequest request)
    {
        var table = request.TableNumber?.Trim();
        if (string.IsNullOrEmpty(table))
        {
            if (request.PayAtTable) throw new ArgumentException("Pay-at-table requires a table number.");
            return null;
        }

        if (affiliate.BusinessType != BusinessType.Restaurant)
            throw new ArgumentException("Table orders are only available for restaurants.");
        if (table.Length > MaxTableLength || !table.All(c => char.IsLetterOrDigit(c) || c == '-' || c == ' '))
            throw new ArgumentException("Invalid table number.");

        if (request.PayAtTable)
        {
            var since = DateTime.UtcNow.AddHours(-2);
            var pending = await _db.Orders.CountAsync(o =>
                o.AffiliateId == affiliate.Id && o.TableNumber == table &&
                o.PaymentMethod == PayAtTableMethod && o.Status == OrderStatus.Pending &&
                o.CreatedAt >= since);
            if (pending >= MaxPendingPayAtTablePerTable)
                throw new ArgumentException("Too many pending orders for this table. Please ask your server.");
        }

        return table;
    }

    private static readonly Dictionary<string, string> DiaLabelEs = new()
    {
        ["lunes"] = "el lunes", ["martes"] = "el martes", ["miercoles"] = "el miércoles", ["jueves"] = "el jueves",
        ["viernes"] = "el viernes", ["sabado"] = "el sábado", ["domingo"] = "el domingo",
    };

    /// <summary>
    /// Regla de "cerrado": con Horario y zona horaria configurados, si el negocio está cerrado ahora
    /// el pedido debe venir programado (<c>ScheduledFor</c>) para la próxima apertura. Abierto, o
    /// sin horario configurado, devuelve null (pedido para ahora). Lo mismo que la web valida al
    /// instante; esto cubre pestañas abiertas desde hace horas y llamadas directas al API.
    /// </summary>
    private static DateOnly? ResolveSchedule(Affiliate affiliate, CreateOrderRequest request)
    {
        var hours = BusinessHoursCheck.Evaluate(
            JsonArrayField.Parse<HorarioEntryDto>(affiliate.Horario), affiliate.Timezone, DateTime.UtcNow);
        if (!hours.Known || hours.IsOpen) return null;

        if (hours.NextOpenDate is not { } next)
            throw new ArgumentException("Estamos cerrados por ahora y no hay una próxima apertura configurada.");

        var when = hours.DaysAhead switch
        {
            0 => "hoy",
            1 => "mañana",
            _ => hours.NextDayToken is not null && DiaLabelEs.TryGetValue(hours.NextDayToken, out var label) ? label : "la próxima apertura",
        };
        var hint = $"Estamos cerrados. Programa tu pedido para {when} a las {hours.NextOpensAt}.";

        if (!string.IsNullOrWhiteSpace(request.TableNumber))
            throw new ArgumentException("Los pedidos de mesa solo se pueden hacer mientras estamos abiertos.");
        if (!DateOnly.TryParseExact(request.ScheduledFor, "yyyy-MM-dd", out var requested))
            throw new ArgumentException(hint);
        if (requested != next)
            throw new ArgumentException("La fecha del pedido programado ya no es válida. Actualiza la página e inténtalo de nuevo.");
        return requested;
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
        o.Tip,
        o.TableNumber,
        o.ScheduledFor?.ToString("yyyy-MM-dd"),
        o.CollectedAt,
        o.CollectedMethod,
        o.EstimatedReadyAt
    );
}
