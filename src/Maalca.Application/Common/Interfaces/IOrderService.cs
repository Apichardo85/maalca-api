using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IOrderService
{
    /// <summary>
    /// Crea el pedido (siempre, Pending) y, si el afiliado tiene Stripe Connect con cargos
    /// habilitados, además una Checkout Session de cobro directo contra su cuenta conectada.
    /// Llamado desde el storefront público — resuelve por slug, no por id.
    /// </summary>
    Task<CreateOrderResponseDto?> CreateOrderAsync(string affiliateSlug, CreateOrderRequest request);

    Task<IReadOnlyList<OrderDto>> GetOrdersAsync(Guid affiliateId);

    Task<OrderDto?> UpdateStatusAsync(Guid affiliateId, Guid orderId, string status);

    /// <summary>
    /// Confirmación síncrona al volver del Checkout hospedado de Stripe. Verifica el estado real
    /// contra Stripe antes de marcar Paid, nunca confía en el query param de la URL solo. Sigue
    /// existiendo como respaldo inmediato (UX más rápida) aunque ya no sea la única vía —
    /// ConfirmFromWebhookAsync cubre el caso de que el cliente nunca vuelva.
    /// </summary>
    Task<OrderDto?> ConfirmCheckoutAsync(Guid orderId, string checkoutSessionId);

    /// <summary>
    /// Confirmación desde el webhook de Stripe Connect (checkout.session.completed en la cuenta
    /// conectada) — cierra el hueco de pedidos huérfanos: si el cliente paga y cierra la pestaña
    /// antes de volver, esto igual marca el pedido Paid y dispara el email de confirmación.
    /// Idempotente por diseño (solo transiciona si sigue Pending) — puede correr después de
    /// ConfirmCheckoutAsync sin duplicar nada, en cualquier orden.
    /// </summary>
    Task ConfirmFromWebhookAsync(string checkoutSessionId, string? paymentIntentId);
}
