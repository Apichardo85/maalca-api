namespace Maalca.Application.Common.DTOs;

/// <summary>Un renglón de la cotización. Group: "modules" | "package" | "units" | "support" | "minimum" |
/// "setup" | "import" | "tier". TotalUsd siempre es a precio de lista (Nivel 1); el nivel de país se
/// aplica en un renglón "tier" al final.</summary>
public record QuoteLineDto(
    string Group,
    string Token,
    string Label,
    decimal Qty,
    decimal UnitUsd,
    decimal TotalUsd,
    bool IsCourtesy);

public record QuoteResultDto(
    string? Country,
    int Tier,
    string TierName,
    decimal TierFactor,
    bool IsCorporate,
    int Locations,
    string? PackageName,            // "Growth" | "Professional" | null
    decimal PackageSavingsUsd,      // lo que ahorra el paquete frente a módulos sueltos
    IReadOnlyList<QuoteLineDto> MonthlyLines,
    decimal MonthlyListUsd,         // Nivel 1
    decimal MonthlyUsd,             // con nivel de país, redondeado
    IReadOnlyList<QuoteLineDto> SetupLines,
    decimal SetupListUsd,
    decimal SetupUsd);

/// <summary>Cotización de un prospecto sin espacio: módulos elegidos a mano.</summary>
public record QuoteSelectionRequest(
    IReadOnlyList<string> Modules,
    string? Country,
    bool IsCorporate,
    int Locations = 1,
    int Vehicles = 0,
    int Users = 0,
    int ContractClients = 0,
    int ImportClients = 0);

public record PricingModuleDto(string Token, string Es, string En, string Tier, decimal Weight, decimal ShareUsd, decimal SoloUsd, decimal SetupUsd);
public record PricingTierDto(int Tier, string Name, decimal Factor, decimal RoundTo);
public record PricingCountryDto(string Country, int Tier);
public record PricingSettingsDto(
    decimal GrowthUsd, decimal ProfessionalUsd, decimal SoloMarkup, decimal CorporateMinUsd,
    decimal SetupBaseUsd, decimal SetupGrowthPerWeightUsd, decimal SetupProPerWeightUsd,
    int IncludedUsers, int IncludedClients, decimal ExtraLocationFactor);

/// <summary>Todo lo que /ops/precios necesita para mostrar y editar la fórmula.</summary>
public record PricingConfigDto(
    IReadOnlyList<PricingModuleDto> Modules,
    IReadOnlyList<PricingTierDto> Tiers,
    IReadOnlyList<PricingCountryDto> Countries,
    PricingSettingsDto Settings);

public record UpdateModuleWeightsRequest(IReadOnlyList<ModuleWeightItem> Items);
public record ModuleWeightItem(string Token, decimal Weight);
public record UpdateTiersRequest(IReadOnlyList<PricingTierDto> Tiers);
public record UpdateCountriesRequest(IReadOnlyList<PricingCountryDto> Countries);
public record SetCorporateRequest(bool IsCorporate, int Locations);

/// <summary>Un módulo suelto en el libro de precios público, ya con el nivel del país.</summary>
public record PublicModulePriceDto(string Token, LocalizedTextDto Name, LocalizedTextDto Desc, string Tier, decimal SoloUsd, decimal ListUsd);
public record PublicPlanPriceDto(string Id, string Name, decimal MonthlyUsd, decimal ListUsd, decimal YearlyUsd, decimal SetupUsd, decimal SetupListUsd);

/// <summary>Libro de precios para /servicios: planes, módulos y extras con el nivel de un país aplicado.</summary>
public record PublicPriceBookDto(
    string Country,
    int Tier,
    string TierName,
    decimal TierFactor,
    IReadOnlyList<PublicPlanPriceDto> Plans,
    IReadOnlyList<PublicModulePriceDto> Modules,
    IReadOnlyList<PlatformAddOnPublicDto> AddOns,
    decimal CorporateFromUsd,
    decimal CorporateFromListUsd,
    decimal VehicleUsd,
    decimal UserUsd,
    decimal ClientsBlockUsd,
    decimal ExtraLocationPercent);

/// <summary>Precio del botón "Mejorar" para un espacio, con el nivel de su país.</summary>
public record UpgradePriceDto(string Country, int Tier, decimal GrowthMonthlyUsd, decimal ProfessionalMonthlyUsd, decimal GrowthListUsd, decimal ProfessionalListUsd);
