namespace Maalca.Domain.Entities;

/// <summary>
/// Peso de un módulo dentro de la bolsa de su plan (Growth o Professional). Decide qué parte de
/// los $38 / $57 le toca. Token sin fila = peso 1. Se edita en /ops/precios.
/// </summary>
public class ModulePricing
{
    public string Token { get; set; } = string.Empty;
    public decimal Weight { get; set; } = 1m;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Nivel de precio por país. Tier 1 = lista (factor 1). Tier 2 = RD y Latinoamérica (factor &lt; 1).
/// Siempre en USD: el nivel baja el precio, no cambia la moneda.
/// </summary>
public class PriceTier
{
    public int Tier { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Factor { get; set; } = 1m;
    /// <summary>Redondeo del precio final (ej. 5 = al múltiplo de $5 más cercano).</summary>
    public decimal RoundTo { get; set; } = 1m;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>País (ISO 3166-1 alpha-2) → nivel de precio. País sin fila = Tier 1.</summary>
public class CountryTier
{
    public string Country { get; set; } = string.Empty;
    public int Tier { get; set; } = 1;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>Ajustes generales de precio (una sola fila, Id = 1).</summary>
public class PricingSettings
{
    public int Id { get; set; } = 1;
    public decimal GrowthUsd { get; set; } = 38m;
    public decimal ProfessionalUsd { get; set; } = 95m;
    /// <summary>Multiplicador del módulo suelto frente a su parte del paquete.</summary>
    public decimal SoloMarkup { get; set; } = 1.3m;
    public decimal CorporateMinUsd { get; set; } = 350m;
    public decimal SetupBaseUsd { get; set; } = 150m;
    public decimal SetupGrowthPerWeightUsd { get; set; } = 40m;
    public decimal SetupProPerWeightUsd { get; set; } = 120m;
    public int IncludedUsers { get; set; } = 3;
    public int IncludedClients { get; set; } = 100;
    /// <summary>Cada local adicional cuesta este % de la base (0.5 = 50%).</summary>
    public decimal ExtraLocationFactor { get; set; } = 0.5m;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
