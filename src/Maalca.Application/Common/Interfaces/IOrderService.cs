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
    /// Confirmación síncrona al volver del Checkout hospedado de Stripe (v1: sin webhook
    /// dedicado a pagos de Connect — ver comentario en OrderService). Verifica el estado real
    /// contra Stripe antes de marcar Paid, nunca confía en el query param de la URL solo.
    /// </summary>
    Task<OrderDto?> ConfirmCheckoutAsync(Guid orderId, string checkoutSessionId);
}
