namespace Maalca.Application.Common.DTOs;

/// <summary>Vista completa para el dashboard del dueño (incluye VIN y visibilidad).</summary>
public record VehicleListingDto(
    Guid Id,
    string Make,
    string Model,
    string? Trim,
    int Year,
    string Condition,
    decimal Price,
    decimal? CompareAtPrice,
    int? Mileage,
    string MileageUnit,
    string? Transmission,
    string? FuelType,
    string? BodyType,
    string? Drivetrain,
    string? ExteriorColor,
    string? InteriorColor,
    string? Engine,
    int? Doors,
    int? Passengers,
    string? Vin,
    string? StockNumber,
    string? ImportOrigin,
    string Status,
    bool Featured,
    bool IsPubliclyVisible,
    int SortOrder,
    bool IsDemo,
    string? ImageUrl,
    IReadOnlyList<string> Images,
    string? Description,
    string? DescriptionEn,
    DateTime CreatedAt
);

/// <summary>
/// Alta y edición (PUT reemplaza todos los campos editables, igual que CommunityProgram). Make,
/// Model, Year y Price son obligatorios; el resto se valida contra listas en VehicleListingService.
/// </summary>
public record UpsertVehicleListingRequest(
    string Make,
    string Model,
    int Year,
    decimal Price,
    string? Trim = null,
    string? Condition = null,
    decimal? CompareAtPrice = null,
    int? Mileage = null,
    string? MileageUnit = null,
    string? Transmission = null,
    string? FuelType = null,
    string? BodyType = null,
    string? Drivetrain = null,
    string? ExteriorColor = null,
    string? InteriorColor = null,
    string? Engine = null,
    int? Doors = null,
    int? Passengers = null,
    string? Vin = null,
    string? StockNumber = null,
    string? ImportOrigin = null,
    string? Status = null,
    bool Featured = false,
    bool IsPubliclyVisible = true,
    int SortOrder = 0,
    List<string>? Images = null,
    string? Description = null,
    string? DescriptionEn = null
);

/// <summary>
/// Configuración del dealer (Affiliate.Settings -> "dealer"). Todo la decide el dueño en el dashboard.
///  - PriceDisplay: "base" (la moneda en que guardó los precios) | "usd" | "dop" | "both".
///  - UsdToDop: pesos por dólar; hace falta para convertir. Sin tasa válida la vitrina muestra solo la moneda base.
///  - MileageUnit: "km" | "mi" — unidad en que la vitrina muestra el millaje.
///  - ShowReserved: true = los reservados se ven con insignia; false = se ocultan.
///  - Financiamiento (Fase 2): FinancingEnabled prende la calculadora; FinancingRate es la tasa ANUAL en %
///    (0 = sin interés); FinancingTerms son los plazos en meses que se ofrecen; MinDownPercent el inicial mínimo.
///  - TradeInEnabled / TestDriveEnabled prenden los formularios "Vendo mi carro" y "Probar este vehículo".
///    Todo arranca apagado: el dueño decide qué leads quiere recibir.
/// </summary>
public record DealerSettingsDto(
    string PriceDisplay,
    decimal? UsdToDop,
    string MileageUnit,
    bool ShowReserved,
    bool FinancingEnabled = false,
    decimal? FinancingRate = null,
    List<int>? FinancingTerms = null,
    decimal MinDownPercent = 20m,
    bool TradeInEnabled = false,
    bool TestDriveEnabled = false);

/// <summary>Vehículo en la vitrina pública: sin VIN, sin visibilidad ni datos internos.</summary>
public record PublicVehicleDto(
    Guid Id,
    string Make,
    string Model,
    string? Trim,
    int Year,
    string Condition,
    decimal Price,
    decimal? CompareAtPrice,
    int? Mileage,
    string MileageUnit,
    string? Transmission,
    string? FuelType,
    string? BodyType,
    string? Drivetrain,
    string? ExteriorColor,
    string? InteriorColor,
    string? Engine,
    int? Doors,
    int? Passengers,
    string? StockNumber,
    string? ImportOrigin,
    string Status,
    bool Featured,
    string? ImageUrl,
    IReadOnlyList<string> Images,
    string? Description,
    string? DescriptionEn,
    DateTime CreatedAt
);

/// <summary>Respuesta de GET /api/public/affiliates/{slug}/dealer-vehicles. BaseCurrency = Affiliate.Currency (en que está Price).</summary>
public record PublicVehiclesResponseDto(string BaseCurrency, DealerSettingsDto Settings, IReadOnlyList<PublicVehicleDto> Vehicles);

// ── Leads (Fase 2) ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Alta pública de un interesado (POST /api/public/affiliates/{slug}/dealer-leads). Kind decide qué campos aplican:
///  - "inquiry": consulta sobre un vehículo (o general).
///  - "financing": exige VehicleId, DownPayment y TermMonths; la cuota la calcula el servidor.
///  - "tradein": exige TradeMake, TradeModel y TradeYear; VehicleId opcional (el que quiere comprar).
///  - "testdrive": exige VehicleId; PreferredAt opcional.
/// DownPayment va en la moneda base del negocio (la que devuelve BaseCurrency en la vitrina).
/// </summary>
public record CreateVehicleLeadRequest(
    string Kind,
    string Name,
    string? Phone = null,
    string? Email = null,
    string? Message = null,
    Guid? VehicleId = null,
    decimal? DownPayment = null,
    int? TermMonths = null,
    string? TradeMake = null,
    string? TradeModel = null,
    int? TradeYear = null,
    int? TradeMileage = null,
    string? TradeMileageUnit = null,
    string? TradeCondition = null,
    List<string>? TradeImages = null,
    DateTime? PreferredAt = null
);

/// <summary>Respuesta del alta pública: solo confirma. No devuelve datos internos del lead.</summary>
public record PublicVehicleLeadResultDto(Guid Id, decimal? MonthlyEstimate, string? Currency);

/// <summary>Vista del dueño en la bandeja de interesados.</summary>
public record VehicleLeadDto(
    Guid Id,
    Guid? VehicleListingId,
    string? VehicleSummary,
    string Kind,
    string Name,
    string? Phone,
    string? Email,
    string? Message,
    string? Currency,
    decimal? DownPayment,
    int? TermMonths,
    decimal? MonthlyEstimate,
    string? TradeMake,
    string? TradeModel,
    int? TradeYear,
    int? TradeMileage,
    string? TradeMileageUnit,
    string? TradeCondition,
    IReadOnlyList<string> TradeImages,
    DateTime? PreferredAt,
    string Status,
    string? InternalNotes,
    DateTime CreatedAt
);

/// <summary>Cambio de estado y notas internas de un lead (único PUT que tiene el dueño sobre ellos).</summary>
public record UpdateVehicleLeadRequest(string Status, string? InternalNotes = null);
