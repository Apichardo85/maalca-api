using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Entrega hecha en una parada. Los envases (llenos entregados, vacíos recogidos, dañados) viven como
/// <see cref="ContainerMovement"/> con este DeliveryId; los productos vendidos, en <see cref="DeliveryItem"/>;
/// y el cobro en ruta, como <see cref="Payment"/>.
/// </summary>
public class Delivery : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public Guid RouteStopId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime DeliveredAt { get; set; } = DateTime.UtcNow;
    /// <summary>Factura generada por esta entrega (si el negocio factura por entrega). Sin FK todavía.</summary>
    public Guid? InvoiceId { get; set; }
    /// <summary>Pago cobrado en el momento, si hubo. Sin FK todavía.</summary>
    public Guid? PaymentId { get; set; }
    /// <summary>Quién recibió en el cliente (nombre), como constancia.</summary>
    public string? ReceivedBy { get; set; }
    public string? Notes { get; set; }
    public string? RegisteredBy { get; set; }
    /// <summary>Evita duplicar la entrega si la PWA reintenta con mala señal. Único por negocio.</summary>
    public string? IdempotencyKey { get; set; }

    public RouteStop? RouteStop { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<DeliveryItem> Items { get; set; } = new List<DeliveryItem>();
}
