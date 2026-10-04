namespace Maalca.Application.Common.DTOs;

/// <param name="Notes">
/// Personalización de esta línea (ej. "sin cebolla, extra queso") — distinto de Order.Notes,
/// que son instrucciones generales del pedido completo.
/// </param>
public record OrderItemDto(string ItemId, string Name, decimal Price, int Qty, string? Notes = null);

/// <param name="SuccessUrl">A dónde vuelve el cliente si el pago se completó (Checkout mode=payment).</param>
/// <param name="CancelUrl">A dónde vuelve el cliente si canceló el pago.</param>
/// <param name="Tip">Propina — Restaurante. 0 si el negocio no la ofrece o el cliente no dejó.</param>
/// <param name="TableNumber">Mesa (QR por mesa). Solo Restaurante; null = pedido normal.</param>
/// <param name="ScheduledFor">"yyyy-MM-dd": pedido programado para la próxima apertura. Obligatorio cuando el negocio está cerrado (y tiene Horario + zona horaria); se ignora si está abierto.</param>
/// <param name="PayAtPickup">true = pedido para recoger pagando en el local (sin Stripe, sin mesa): exige nombre y teléfono o correo; queda Pending hasta que el personal lo acepta.</param>
/// <param name="PayAtTable">true = el cliente paga al mesero (requiere TableNumber): sin Stripe, queda Pending hasta que el personal lo acepta.</param>
public record CreateOrderRequest(
    IReadOnlyList<OrderItemDto> Items,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    string? CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    string? Notes,
    string? Currency,
    string? SuccessUrl,
    string? CancelUrl,
    decimal Tip = 0,
    string? TableNumber = null,
    bool PayAtTable = false,
    string? ScheduledFor = null,
    bool PayAtPickup = false
);

/// <param name="CheckoutUrl">
/// Null si el afiliado todavía no tiene Stripe Connect activo (ChargesEnabled=false) — en ese
/// caso el pedido se guarda igual como Pending, y el storefront debe caer al flujo de
/// WhatsApp existente en vez de intentar cobrar.
/// </param>
public record CreateOrderResponseDto(Guid OrderId, string? CheckoutUrl, string? TrackingToken = null);

public record OrderDto(
    Guid Id,
    string? CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    string? Notes,
    IReadOnlyList<OrderItemDto> Items,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    string Currency,
    string Status,
    DateTime CreatedAt,
    string Channel = "Online",
    string? PaymentMethod = null,
    decimal Tip = 0,
    string? TableNumber = null,
    string? ScheduledFor = null,
    DateTime? CollectedAt = null,
    string? CollectedMethod = null,
    DateTime? EstimatedReadyAt = null
);

public record UpdateOrderStatusRequest(string Status, int? EstimatedMinutes = null);

/// <param name="Minutes">Minutos desde ahora hasta que el pedido estará listo (1–240).</param>
public record SetOrderEtaRequest(int Minutes);

/// <summary>Vista pública (sin login) para /t/{token}: solo lo que el cliente necesita ver de su pedido.</summary>
public record OrderTrackingDto(
    string BusinessName,
    string Slug,
    string? LogoUrl,
    string? BrandColor,
    string? Address,
    string? WhatsApp,
    string Status,
    bool PayAtVenue,
    bool Collected,
    string? TableNumber,
    string? ScheduledFor,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? EstimatedReadyAt,
    IReadOnlyList<OrderItemDto> Items,
    decimal Subtotal,
    decimal Tax,
    decimal Tip,
    decimal Total,
    string Currency,
    bool Expired,
    bool CanPayOnline = false);

public record TrackingPayRequest(string SuccessUrl, string CancelUrl);

public record TrackingPayResponseDto(string CheckoutUrl);

public record TrackingConfirmRequest(string SessionId);

/// <param name="Method">"Cash" | "Card" | "Other" — cómo se cobró en el local.</param>
public record CollectOrderPaymentRequest(string Method);

public record ConfirmOrderRequest(string CheckoutSessionId);

/// <summary>
/// POS (Etapa D, fase 1) — venta presencial registrada desde el dashboard. A diferencia de
/// CreateOrderRequest (storefront público, pasa por Stripe Checkout), esto entra directo como
/// Paid: el cobro real (efectivo, tarjeta externa, etc.) ya ocurrió en el mostrador, el POS
/// solo lo deja constando. PaymentMethod: "Cash" | "Card" | "Other".
/// </summary>
public record CreatePosOrderRequest(
    IReadOnlyList<OrderItemDto> Items,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    string? CustomerName,
    string? Notes,
    string? Currency,
    string PaymentMethod,
    decimal Tip = 0
);

/// <summary>
/// POS con cobro real de Stripe (Etapa D, fase 2) — a diferencia de CreatePosOrderRequest
/// (Cash/Other, entra directo Paid porque el cobro ya pasó por fuera), esto genera una Checkout
/// Session con la cuenta Connect del negocio (mismo direct charge que el storefront público) y
/// el pedido queda Pending hasta que el cliente paga desde su propio teléfono (QR/link) — el
/// webhook de Connect confirma el pago igual que en CreateOrderAsync. SuccessUrl/CancelUrl
/// apuntan a una página pública genérica de "gracias" (no requiere volver al POS: el POS se
/// entera del pago vía SignalR, no por la redirección).
/// </summary>
public record CreatePosCheckoutRequest(
    IReadOnlyList<OrderItemDto> Items,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    string? CustomerName,
    string? Notes,
    string? Currency,
    string SuccessUrl,
    string CancelUrl,
    decimal Tip = 0
);
