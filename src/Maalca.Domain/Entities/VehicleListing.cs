using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Un vehículo en venta de un negocio tipo Dealer (autoventa / concesionario). Tabla propia y no
/// InventoryItem + JSON porque el cliente final filtra por varias columnas a la vez (carrocería,
/// marca, año, rango de precio), y eso necesita columnas con índice. Mismo patrón que
/// CommunityProgram: entidad propia, su CRUD en /space/[slug]/vehicles, su endpoint público.
///
/// Los valores "token" (Condition, Status, Transmission, FuelType, BodyType, Drivetrain,
/// ImportOrigin) son claves en inglés en minúscula; la web las traduce a etiquetas es/en. La lista
/// permitida la valida VehicleListingService, no la base de datos.
///
/// Price se guarda en la moneda del negocio (Affiliate.Currency); cómo se muestra (USD, DOP o
/// ambas) lo decide el dueño en Affiliate.Settings -> "dealer" (ver DealerSettingsDto).
/// </summary>
public class VehicleListing : AuditableEntity
{
    public Guid AffiliateId { get; set; }

    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    /// <summary>Versión / acabado (ej. "GLX", "XLE", "Limited").</summary>
    public string? Trim { get; set; }
    public int Year { get; set; }
    /// <summary>"new" | "used" | "certified".</summary>
    public string Condition { get; set; } = "used";

    public decimal Price { get; set; }
    /// <summary>Precio "antes" tachado. Solo es oferta si es mayor que Price.</summary>
    public decimal? CompareAtPrice { get; set; }

    public int? Mileage { get; set; }
    /// <summary>"km" | "mi" — la unidad en que se registró ESTE vehículo; la vitrina lo convierte a la del negocio.</summary>
    public string MileageUnit { get; set; } = "km";

    /// <summary>"automatic" | "manual".</summary>
    public string? Transmission { get; set; }
    /// <summary>"gasoline" | "diesel" | "hybrid" | "electric" | "other".</summary>
    public string? FuelType { get; set; }
    /// <summary>"suv" | "jeep" | "sedan" | "hatchback" | "crossover" | "pickup" | "van" | "coupe" | "bus" | "other".</summary>
    public string? BodyType { get; set; }
    /// <summary>"fwd" | "rwd" | "awd" | "4x4".</summary>
    public string? Drivetrain { get; set; }
    public string? ExteriorColor { get; set; }
    public string? InteriorColor { get; set; }
    /// <summary>Motor en texto libre (ej. "2.5L · 4 cil.").</summary>
    public string? Engine { get; set; }
    public int? Doors { get; set; }
    public int? Passengers { get; set; }

    /// <summary>VIN — solo dashboard; no viaja en el endpoint público.</summary>
    public string? Vin { get; set; }
    /// <summary>Número de stock interno del dealer.</summary>
    public string? StockNumber { get; set; }
    /// <summary>"usa" | "korea" | "japan" | "local" | "other".</summary>
    public string? ImportOrigin { get; set; }

    /// <summary>"available" | "reserved" | "sold". Vendido nunca se muestra; reservado depende de Settings.dealer.showReserved.</summary>
    public string Status { get; set; } = "available";
    public bool Featured { get; set; } = false;
    public bool IsPubliclyVisible { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public bool IsDemo { get; set; } = false;

    public string? ImageUrl { get; set; }
    /// <summary>JSON array de URLs; Images[0] == ImageUrl (mismo patrón que InventoryItem).</summary>
    public string? Images { get; set; }
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }

    public Affiliate? Affiliate { get; set; }
}
