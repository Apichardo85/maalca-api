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
    /// <summary>Compras sin impuesto de venta (reventa con certificado, ej. ST-120 en NY). El dueño valida el certificado.</summary>
    public bool TaxExempt { get; set; } = false;
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
    /// <summary>Tarifa por peso del pedido, en libras: "hasta N lb : precio" separados por ';' (ej. "1:6.50; 3:9.99; 10:18").
    /// Null = costo fijo (Price). Sin peso conocido en los productos se usa Price.</summary>
    public string? WeightRates { get; set; }
    /// <summary>Estados (2 letras, separados por coma) a los que aplica el método. Null/vacío = todo el país.</summary>
    public string? States { get; set; }

    public Affiliate? Affiliate { get; set; }
}

/// <summary>
/// Cuenta de cliente DENTRO de una tienda: la misma persona (usuario de Supabase) puede ser miembro de varias
/// tiendas, con estado y lista de precios distintos en cada una. El precio nunca sale del token: sale de aquí.
/// Status: "pending" (esperando al dueño) | "approved" | "rejected".
/// </summary>
public class StoreCustomer : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public string SupabaseUserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? BusinessName { get; set; }
    public string? TaxId { get; set; }
    /// <summary>Lo que el cliente escribió al pedir la membresía.</summary>
    public string? Message { get; set; }
    public string Status { get; set; } = "pending";
    public Guid? PriceListId { get; set; }
    /// <summary>Notas internas del dueño (el cliente no las ve).</summary>
    public string? Notes { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Affiliate? Affiliate { get; set; }
    public PriceList? PriceList { get; set; }
}
