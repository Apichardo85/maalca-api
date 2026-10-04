using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Dispositivo del CLIENTE que pidió "Avísame" en su página de seguimiento (/t/{token}): recibe push cuando el
/// pedido se acepta, se retrasa, está listo o se cancela. Ligado al pedido (no al negocio): si el pedido se borra,
/// la suscripción también. Mismo formato que PushSubscription (la web hace el envío con las llaves VAPID).
/// </summary>
public class OrderPushSubscription : AuditableEntity
{
    public Guid OrderId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public string Lang { get; set; } = "es";

    public Order? Order { get; set; }
}
