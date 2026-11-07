using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Movimiento de envases con un cliente. El saldo del cliente (llenos en su poder, vacíos pendientes de recoger)
/// se calcula sumando estos movimientos; no se guarda aparte para que nunca se desfase.
/// </summary>
public class ContainerMovement : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ContainerTypeId { get; set; }
    public Guid? AnaquelId { get; set; }
    /// <summary>Reservado para la entrega de ruta (Fase 2, S14+). Sin FK todavía.</summary>
    public Guid? DeliveryId { get; set; }
    /// <summary>Reservado para trazabilidad por botellón individual. Null mientras se maneje por cantidades.</summary>
    public Guid? BottleId { get; set; }
    /// <summary>"delivery" (lleno entregado) | "return" (vacío recogido) | "damaged" (dañado/perdido) | "adjustment" (corrección, cantidad con signo).</summary>
    public string Type { get; set; } = "delivery";
    /// <summary>Siempre positiva salvo en "adjustment", donde el signo indica el sentido.</summary>
    public int Quantity { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? RegisteredBy { get; set; }
    public string? Notes { get; set; }
    /// <summary>Evita duplicar el movimiento si la PWA reintenta (mala señal en ruta). Único por negocio.</summary>
    public string? IdempotencyKey { get; set; }

    public Affiliate? Affiliate { get; set; }
    public Customer? Customer { get; set; }
    public ContainerType? ContainerType { get; set; }
    public Anaquel? Anaquel { get; set; }
}
