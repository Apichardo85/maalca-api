using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Tipo de envase retornable que se presta al cliente (ej. "Botellón 5 gal"). Es un activo del negocio,
/// no stock consumible como <see cref="InventoryItem"/>: se entrega lleno, vuelve vacío y se lleva la cuenta.
/// </summary>
public class ContainerType : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Capacidad en litros (informativa). Null = no aplica.</summary>
    public decimal? CapacityLiters { get; set; }
    /// <summary>Depósito en garantía por envase prestado. 0 = sin depósito.</summary>
    public decimal Deposit { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public Affiliate? Affiliate { get; set; }
}
