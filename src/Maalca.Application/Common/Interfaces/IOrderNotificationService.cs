using Maalca.Domain.Entities;

namespace Maalca.Application.Common.Interfaces;

/// <summary>
/// Automatización básica: confirmaciones/avisos de pedido al cliente final. MaalCa-api no
/// manda emails directamente (no hay proveedor configurado en .NET) — reusa la infraestructura
/// de Resend que ya vive en maalca-web llamando a un endpoint interno protegido por secreto
/// compartido. Ver notifications/order route.ts en maalca-web.
/// </summary>
public interface IOrderNotificationService
{
    /// <summary>Cliente pagó — dispara desde OrderService.ConfirmCheckoutAsync.</summary>
    Task NotifyOrderConfirmedAsync(Order order);

    /// <summary>Afiliado marcó el pedido como Fulfilled — dispara desde UpdateStatusAsync.</summary>
    Task NotifyOrderFulfilledAsync(Order order);
}
