using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Lista de precios del negocio (ej. "Miembro", "Mayorista"): un descuento general sobre el precio público
/// y, opcionalmente, un precio fijo por producto (PriceListItem) que manda sobre el descuento. El precio final
/// siempre lo calcula el backend al crear el pedido (PriceListPricing), nunca el navegador.
/// Quién recibe qué lista lo decide la cuenta de cliente de la tienda (pendiente: cuentas de clientes).
/// </summary>
public class PriceList : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>0–100. Se aplica a todo producto sin precio fijo en esta lista.</summary>
    public decimal DiscountPercent { get; set; } = 0;
    /// <summary>Pedido mínimo (subtotal) para comprar con esta lista. Null = sin mínimo.</summary>
    public decimal? MinOrderAmount { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public Affiliate? Affiliate { get; set; }
    public ICollection<PriceListItem> Items { get; set; } = new List<PriceListItem>();
}

/// <summary>Precio fijo de un producto dentro de una lista. ItemId = InventoryItem (o Product/Service) del negocio.</summary>
public class PriceListItem : BaseEntity
{
    public Guid PriceListId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Price { get; set; }

    public PriceList? PriceList { get; set; }
}

/// <summary>
/// Método de envío del negocio, configurado a mano mientras no hay agregador (EasyPost/Shippo):
/// costo fijo, opcionalmente gratis desde cierto subtotal. AllowsHazmat = el método es terrestre
/// (USPS Ground Advantage, UPS/FedEx Ground) y puede llevar perfumes en cantidad limitada.
/// </summary>
public class ShippingMethod : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public decimal Price { get; set; }
    public decimal? FreeOverAmount { get; set; }
    public int? MinDays { get; set; }
    public int? MaxDays { get; set; }
    public bool AllowsHazmat { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public Affiliate? Affiliate { get; set; }
}
