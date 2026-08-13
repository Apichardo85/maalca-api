using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>
/// Push en tiempo real para el Kitchen Display (/space/{slug}/kitchen). Abstracción sobre
/// SignalR — la implementación concreta (que envuelve IHubContext&lt;OrdersHub&gt;) vive en
/// Maalca.Api, ya que Application no puede depender de Api (rompería la capa). Mismo patrón que
/// IOrderNotificationService para las notificaciones por email.
/// </summary>
public interface IOrderRealtimeNotifier
{
    /// <summary>Un pedido cambió de estado (o se confirmó el pago) — refresca el board.</summary>
    Task NotifyOrderUpdatedAsync(Guid affiliateId, OrderDto order);
}
