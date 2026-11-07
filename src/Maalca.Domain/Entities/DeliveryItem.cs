using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>Producto entregado y su precio en el momento (el precio acordado del contrato puede cambiar después).</summary>
public class DeliveryItem : BaseEntity
{
    public Guid DeliveryId { get; set; }
    /// <summary>Producto del catálogo (<see cref="InventoryItem"/>). Sin FK: el historial no debe depender del catálogo.</summary>
    public Guid? ItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public Delivery? Delivery { get; set; }
}
