namespace Maalca.Application.Common.DTOs;

public record OrderItemDto(string ItemId, string Name, decimal Price, int Qty);

/// <param name="SuccessUrl">A dónde vuelve el cliente si el pago se completó (Checkout mode=payment).</param>
/// <param name="CancelUrl">A dónde vuelve el cliente si canceló el pago.</param>
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
    string? CancelUrl
);

/// <param name="CheckoutUrl">
/// Null si el afiliado todavía no tiene Stripe Connect activo (ChargesEnabled=false) — en ese
/// caso el pedido se guarda igual como Pending, y el storefront debe caer al flujo de
/// WhatsApp existente en vez de intentar cobrar.
/// </param>
public record CreateOrderResponseDto(Guid OrderId, string? CheckoutUrl);

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
    string? PaymentMethod = null
);

public record UpdateOrderStatusRequest(string Status);

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
    string PaymentMethod
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
    string CancelUrl
);
