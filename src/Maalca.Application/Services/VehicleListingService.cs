using System.Text.Json;
using System.Text.Json.Nodes;
using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

/// <summary>
/// Inventario de vehículos del tipo de negocio Dealer. Mismo patrón que CommunityProgramService
/// (CRUD por afiliado) más la configuración del dealer, que vive en Affiliate.Settings -> "dealer"
/// para no agregar columnas: el dueño decide moneda (USD/DOP/ambas), unidad de millaje y si los
/// reservados se muestran. Esas reglas las aplica el SERVIDOR en la lista pública -- un vehículo que
/// el dueño oculta nunca viaja en el JSON.
/// </summary>
public class VehicleListingService : IVehicleListingService
{
    // Máximo de vehículos en la respuesta pública. La web filtra en el cliente sobre esta lista;
    // si un negocio la supera hay que pasar los filtros al servidor (ver plans/spec-espacio-dealer.md §3.6).
    private const int PublicMax = 300;
    private const int MaxImages = 20;

    private static readonly string[] Conditions = { "new", "used", "certified" };
    private static readonly string[] Statuses = { "available", "reserved", "sold" };
    private static readonly string[] Transmissions = { "automatic", "manual" };
    private static readonly string[] FuelTypes = { "gasoline", "diesel", "hybrid", "electric", "other" };
    private static readonly string[] BodyTypes = { "suv", "jeep", "sedan", "hatchback", "crossover", "pickup", "van", "coupe", "bus", "other" };
    private static readonly string[] Drivetrains = { "fwd", "rwd", "awd", "4x4" };
    private static readonly string[] Origins = { "usa", "korea", "japan", "local", "other" };
    private static readonly string[] MileageUnits = { "km", "mi" };
    private static readonly string[] PriceDisplays = { "base", "usd", "dop", "both" };

    // Financiamiento (Fase 2): plazos que se ofrecen si el dueño no eligió los suyos.
    public static readonly int[] DefaultTerms = { 24, 36, 48, 60, 72 };
    private const int MinTermMonths = 6;
    private const int MaxTermMonths = 120;
    private const int MaxTerms = 8;
    private const decimal DefaultMinDownPercent = 20m;

    private readonly AppDbContext _db;

    public VehicleListingService(AppDbContext db) => _db = db;

    // ── Dashboard ────────────────────────────────────────────────────────────────

    public async Task<List<VehicleListingDto>> GetAsync(Guid affiliateId)
    {
        var rows = await _db.VehicleListings
            .AsNoTracking()
            .Where(v => v.AffiliateId == affiliateId)
            .OrderBy(v => v.SortOrder).ThenByDescending(v => v.CreatedAt)
            .ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<VehicleListingDto> CreateAsync(Guid affiliateId, UpsertVehicleListingRequest r)
    {
        var vehicle = new VehicleListing
        {
            Id = Guid.NewGuid(),
            AffiliateId = affiliateId,
            CreatedAt = DateTime.UtcNow,
        };
        Apply(vehicle, r);
        _db.VehicleListings.Add(vehicle);
        await _db.SaveChangesAsync();
        return ToDto(vehicle);
    }

    public async Task<VehicleListingDto?> UpdateAsync(Guid affiliateId, Guid id, UpsertVehicleListingRequest r)
    {
        var vehicle = await _db.VehicleListings.FirstOrDefaultAsync(v => v.Id == id && v.AffiliateId == affiliateId);
        if (vehicle == null) return null;
        Apply(vehicle, r);
        vehicle.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToDto(vehicle);
    }

    public async Task<bool> DeleteAsync(Guid affiliateId, Guid id)
    {
        var vehicle = await _db.VehicleListings.FirstOrDefaultAsync(v => v.Id == id && v.AffiliateId == affiliateId);
        if (vehicle == null) return false;
        _db.VehicleListings.Remove(vehicle);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Configuración del dealer ─────────────────────────────────────────────────

    public async Task<DealerSettingsDto?> GetSettingsAsync(Guid affiliateId)
    {
        var settingsJson = await _db.Affiliates.AsNoTracking()
            .Where(a => a.Id == affiliateId)
            .Select(a => a.Settings)
            .FirstOrDefaultAsync();
        // FirstOrDefault de un string no distingue "no existe" de "Settings vacío": confirmamos aparte.
        if (settingsJson == null && !await _db.Affiliates.AnyAsync(a => a.Id == affiliateId)) return null;
        return ParseSettings(settingsJson);
    }

    public async Task<DealerSettingsDto?> UpdateSettingsAsync(Guid affiliateId, DealerSettingsDto r)
    {
        var a = await _db.Affiliates.FirstOrDefaultAsync(x => x.Id == affiliateId);
        if (a is null) return null;

        var display = (r.PriceDisplay ?? "").Trim().ToLowerInvariant();
        if (!PriceDisplays.Contains(display))
            throw new ArgumentException("Moneda: debe ser base, usd, dop o both.");
        var unit = (r.MileageUnit ?? "").Trim().ToLowerInvariant();
        if (!MileageUnits.Contains(unit))
            throw new ArgumentException("Millaje: la unidad debe ser km o mi.");
        if (r.UsdToDop is { } rate && (rate < 1m || rate > 1000m))
            throw new ArgumentException("Tasa: pesos por dólar debe estar entre 1 y 1000.");
        // Mostrar USD o DOP (o ambas) cuando no es la moneda base exige una tasa para convertir.
        if (display != "base" && r.UsdToDop is null)
            throw new ArgumentException("Tasa: indica cuántos pesos vale un dólar para mostrar los precios en otra moneda.");

        // Financiamiento: si está prendido necesita tasa; los plazos se limpian (únicos, en rango, ordenados).
        if (r.FinancingRate is < 0m or > 100m)
            throw new ArgumentException("Financiamiento: la tasa anual debe estar entre 0 y 100.");
        if (r.FinancingEnabled && r.FinancingRate is null)
            throw new ArgumentException("Financiamiento: indica la tasa de interés anual (0 si es sin interés).");
        if (r.MinDownPercent is < 0m or > 90m)
            throw new ArgumentException("Financiamiento: el inicial mínimo debe estar entre 0% y 90%.");
        var terms = (r.FinancingTerms ?? new List<int>()).Distinct().OrderBy(t => t).ToList();
        if (terms.Any(t => t < MinTermMonths || t > MaxTermMonths))
            throw new ArgumentException($"Financiamiento: los plazos deben estar entre {MinTermMonths} y {MaxTermMonths} meses.");
        if (terms.Count > MaxTerms)
            throw new ArgumentException($"Financiamiento: máximo {MaxTerms} plazos.");
        if (terms.Count == 0) terms = DefaultTerms.ToList();

        // Fusionar sin pisar el resto de Settings (defaultTheme, spanishFlag...). Si el JSON guardado
        // está roto se reemplaza por un objeto nuevo en vez de fallar el guardado.
        JsonObject root;
        try { root = JsonNode.Parse(string.IsNullOrWhiteSpace(a.Settings) ? "{}" : a.Settings) as JsonObject ?? new JsonObject(); }
        catch (JsonException) { root = new JsonObject(); }

        var dealer = new JsonObject
        {
            ["priceDisplay"] = display,
            ["mileageUnit"] = unit,
            ["showReserved"] = r.ShowReserved,
            ["financingEnabled"] = r.FinancingEnabled,
            ["financingTerms"] = new JsonArray(terms.Select(t => (JsonNode?)JsonValue.Create(t)).ToArray()),
            ["minDownPercent"] = r.MinDownPercent,
            ["tradeInEnabled"] = r.TradeInEnabled,
            ["testDriveEnabled"] = r.TestDriveEnabled,
        };
        if (r.UsdToDop is { } usdToDop) dealer["usdToDop"] = usdToDop;
        if (r.FinancingRate is { } financingRate) dealer["financingRate"] = financingRate;
        root["dealer"] = dealer;

        a.Settings = root.ToJsonString();
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return new DealerSettingsDto(display, r.UsdToDop, unit, r.ShowReserved,
            r.FinancingEnabled, r.FinancingRate, terms, r.MinDownPercent, r.TradeInEnabled, r.TestDriveEnabled);
    }

    /// <summary>Lee Settings -> "dealer". Cualquier cosa rara (JSON roto, valor fuera de lista) cae al default.</summary>
    public static DealerSettingsDto ParseSettings(string? settingsJson)
    {
        var display = "base";
        decimal? rate = null;
        var unit = "km";
        var showReserved = true;
        var financingEnabled = false;
        decimal? financingRate = null;
        var terms = DefaultTerms.ToList();
        var minDown = DefaultMinDownPercent;
        var tradeIn = false;
        var testDrive = false;

        if (!string.IsNullOrWhiteSpace(settingsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(settingsJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("dealer", out var d)
                    && d.ValueKind == JsonValueKind.Object)
                {
                    if (d.TryGetProperty("priceDisplay", out var pd) && pd.ValueKind == JsonValueKind.String
                        && pd.GetString()?.ToLowerInvariant() is { } pdv && PriceDisplays.Contains(pdv))
                        display = pdv;
                    if (d.TryGetProperty("mileageUnit", out var mu) && mu.ValueKind == JsonValueKind.String
                        && mu.GetString()?.ToLowerInvariant() is { } muv && MileageUnits.Contains(muv))
                        unit = muv;
                    if (d.TryGetProperty("usdToDop", out var ur) && ur.ValueKind == JsonValueKind.Number
                        && ur.TryGetDecimal(out var rv) && rv >= 1m && rv <= 1000m)
                        rate = rv;
                    if (d.TryGetProperty("showReserved", out var sr)
                        && (sr.ValueKind == JsonValueKind.True || sr.ValueKind == JsonValueKind.False))
                        showReserved = sr.GetBoolean();

                    if (d.TryGetProperty("financingEnabled", out var fe)
                        && (fe.ValueKind == JsonValueKind.True || fe.ValueKind == JsonValueKind.False))
                        financingEnabled = fe.GetBoolean();
                    if (d.TryGetProperty("financingRate", out var fr) && fr.ValueKind == JsonValueKind.Number
                        && fr.TryGetDecimal(out var frv) && frv >= 0m && frv <= 100m)
                        financingRate = frv;
                    if (d.TryGetProperty("minDownPercent", out var md) && md.ValueKind == JsonValueKind.Number
                        && md.TryGetDecimal(out var mdv) && mdv >= 0m && mdv <= 90m)
                        minDown = mdv;
                    if (d.TryGetProperty("financingTerms", out var ft) && ft.ValueKind == JsonValueKind.Array)
                    {
                        var parsed = ft.EnumerateArray()
                            .Where(x => x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out _))
                            .Select(x => x.GetInt32())
                            .Where(x => x >= MinTermMonths && x <= MaxTermMonths)
                            .Distinct().OrderBy(x => x).Take(MaxTerms).ToList();
                        if (parsed.Count > 0) terms = parsed;
                    }
                    if (d.TryGetProperty("tradeInEnabled", out var ti)
                        && (ti.ValueKind == JsonValueKind.True || ti.ValueKind == JsonValueKind.False))
                        tradeIn = ti.GetBoolean();
                    if (d.TryGetProperty("testDriveEnabled", out var td)
                        && (td.ValueKind == JsonValueKind.True || td.ValueKind == JsonValueKind.False))
                        testDrive = td.GetBoolean();
                }
            }
            catch (JsonException) { }
        }

        // Mostrar otra moneda sin tasa válida no se puede convertir: volvemos a la moneda base.
        if (display != "base" && rate is null) display = "base";
        // Financiamiento prendido sin tasa guardada no puede calcular una cuota honesta: queda apagado.
        if (financingEnabled && financingRate is null) financingEnabled = false;
        return new DealerSettingsDto(display, rate, unit, showReserved,
            financingEnabled, financingRate, terms, minDown, tradeIn, testDrive);
    }

    // ── Vitrina pública ──────────────────────────────────────────────────────────

    public async Task<PublicVehiclesResponseDto?> GetPublicAsync(string slug)
    {
        var affiliate = await _db.Affiliates.AsNoTracking()
            .Where(a => a.Slug == slug && a.Published && a.BusinessType == BusinessType.Dealer)
            .Select(a => new { a.Id, a.Settings, a.Currency })
            .FirstOrDefaultAsync();
        if (affiliate is null) return null;

        var settings = ParseSettings(affiliate.Settings);

        var query = _db.VehicleListings.AsNoTracking()
            .Where(v => v.AffiliateId == affiliate.Id && v.IsPubliclyVisible && v.Status != "sold");
        // Reservados: el dueño decide si se ven. Se filtra en SQL para que no viajen si están ocultos.
        if (!settings.ShowReserved)
            query = query.Where(v => v.Status != "reserved");

        var rows = await query
            .OrderByDescending(v => v.Featured).ThenBy(v => v.SortOrder).ThenByDescending(v => v.CreatedAt)
            .Take(PublicMax)
            .ToListAsync();

        var currency = string.Equals(affiliate.Currency, "DOP", StringComparison.OrdinalIgnoreCase) ? "DOP" : "USD";
        return new PublicVehiclesResponseDto(currency, settings, rows.Select(ToPublicDto).ToList());
    }

    // ── Mapeo y validación ───────────────────────────────────────────────────────

    private static VehicleListingDto ToDto(VehicleListing v) => new(
        v.Id, v.Make, v.Model, v.Trim, v.Year, v.Condition, v.Price, v.CompareAtPrice,
        v.Mileage, v.MileageUnit, v.Transmission, v.FuelType, v.BodyType, v.Drivetrain,
        v.ExteriorColor, v.InteriorColor, v.Engine, v.Doors, v.Passengers,
        v.Vin, v.StockNumber, v.ImportOrigin, v.Status, v.Featured, v.IsPubliclyVisible,
        v.SortOrder, v.IsDemo, v.ImageUrl, JsonArrayField.Parse<string>(v.Images),
        v.Description, v.DescriptionEn, v.CreatedAt);

    private static PublicVehicleDto ToPublicDto(VehicleListing v) => new(
        v.Id, v.Make, v.Model, v.Trim, v.Year, v.Condition, v.Price, v.CompareAtPrice,
        v.Mileage, v.MileageUnit, v.Transmission, v.FuelType, v.BodyType, v.Drivetrain,
        v.ExteriorColor, v.InteriorColor, v.Engine, v.Doors, v.Passengers,
        v.StockNumber, v.ImportOrigin, v.Status, v.Featured, v.ImageUrl,
        JsonArrayField.Parse<string>(v.Images), v.Description, v.DescriptionEn, v.CreatedAt);

    /// <summary>Valida y copia los campos editables del request a la entidad. ArgumentException = 400.</summary>
    private static void Apply(VehicleListing v, UpsertVehicleListingRequest r)
    {
        var make = Clip(r.Make, 60);
        var model = Clip(r.Model, 80);
        if (make is null) throw new ArgumentException("Vehículo: la marca es obligatoria.");
        if (model is null) throw new ArgumentException("Vehículo: el modelo es obligatorio.");
        if (r.Year < 1950 || r.Year > DateTime.UtcNow.Year + 2)
            throw new ArgumentException("Vehículo: el año no es válido.");
        if (r.Price < 0 || r.Price > 100_000_000m)
            throw new ArgumentException("Vehículo: el precio no es válido.");
        if (r.CompareAtPrice is < 0 or > 100_000_000m)
            throw new ArgumentException("Vehículo: el precio anterior no es válido.");
        if (r.Mileage is < 0 or > 2_000_000)
            throw new ArgumentException("Vehículo: el millaje no es válido.");
        if (r.Doors is < 1 or > 8)
            throw new ArgumentException("Vehículo: las puertas deben estar entre 1 y 8.");
        if (r.Passengers is < 1 or > 60)
            throw new ArgumentException("Vehículo: los pasajeros deben estar entre 1 y 60.");

        v.Make = make;
        v.Model = model;
        v.Trim = Clip(r.Trim, 80);
        v.Year = r.Year;
        v.Condition = Token(r.Condition, Conditions, "used", "condición");
        v.Price = r.Price;
        v.CompareAtPrice = r.CompareAtPrice;
        v.Mileage = r.Mileage;
        v.MileageUnit = Token(r.MileageUnit, MileageUnits, "km", "unidad de millaje");
        v.Transmission = OptionalToken(r.Transmission, Transmissions, "transmisión");
        v.FuelType = OptionalToken(r.FuelType, FuelTypes, "combustible");
        v.BodyType = OptionalToken(r.BodyType, BodyTypes, "carrocería");
        v.Drivetrain = OptionalToken(r.Drivetrain, Drivetrains, "tracción");
        v.ExteriorColor = Clip(r.ExteriorColor, 40);
        v.InteriorColor = Clip(r.InteriorColor, 40);
        v.Engine = Clip(r.Engine, 60);
        v.Doors = r.Doors;
        v.Passengers = r.Passengers;
        v.Vin = Clip(r.Vin, 20)?.ToUpperInvariant();
        v.StockNumber = Clip(r.StockNumber, 40);
        v.ImportOrigin = OptionalToken(r.ImportOrigin, Origins, "origen");
        v.Status = Token(r.Status, Statuses, "available", "estado");
        v.Featured = r.Featured;
        v.IsPubliclyVisible = r.IsPubliclyVisible;
        v.SortOrder = r.SortOrder;
        v.Description = Clip(r.Description, 2000);
        v.DescriptionEn = Clip(r.DescriptionEn, 2000);

        // Galería: Images[0] es la portada (ImageUrl). Solo URLs http(s) -- lo demás se descarta.
        var images = (r.Images ?? new List<string>())
            .Select(u => u?.Trim() ?? "")
            .Where(u => u.Length > 0 && u.Length <= 500 && (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            .Distinct()
            .Take(MaxImages)
            .ToList();
        v.Images = images.Count == 0 ? null : JsonArrayField.Serialize(images);
        v.ImageUrl = images.Count == 0 ? null : images[0];
    }

    private static string? Clip(string? value, int max)
    {
        var t = value?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length > max ? t[..max] : t;
    }

    /// <summary>Token obligatorio con default: vacío = default; fuera de la lista = error.</summary>
    private static string Token(string? value, string[] allowed, string fallback, string label)
    {
        var t = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(t)) return fallback;
        if (!allowed.Contains(t)) throw new ArgumentException($"Vehículo: {label} no válido.");
        return t;
    }

    /// <summary>Token opcional: vacío = null; fuera de la lista = error.</summary>
    private static string? OptionalToken(string? value, string[] allowed, string label)
    {
        var t = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(t)) return null;
        if (!allowed.Contains(t)) throw new ArgumentException($"Vehículo: {label} no válido.");
        return t;
    }
}
