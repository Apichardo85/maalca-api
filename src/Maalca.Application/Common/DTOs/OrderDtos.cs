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
    DateTime CreatedAt
);

public record UpdateOrderStatusRequest(string Status);

public record ConfirmOrderRequest(string CheckoutSessionId);
