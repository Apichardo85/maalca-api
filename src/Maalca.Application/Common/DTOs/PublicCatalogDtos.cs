namespace Maalca.Application.Common.DTOs;

public record AffiliatePublicDto(
    Guid Id,
    string Name,
    string Slug,
    string BusinessType,
    string? Description,
    string? DescriptionEn,
    string? PrimaryColor,
    string? LogoUrl,
    string? CoverImageUrl,
    string? WhatsApp,
    string? ContactEmail,
    string? Address,
    string? City,       // not on Affiliate entity yet — always null until migration adds it
    string? Website,
    List<CanalDto> Canales,
    List<ProcessStepDto> ProcessSteps,
    List<FaqItemDto> Faq,
    List<HorarioEntryDto> Horario,
    string? Timezone,
    string Currency = "USD",   // "USD" | "DOP" — cómo el negocio muestra sus precios
    // Clave ausente = visible (default true). Las plantillas ya ocultan una sección sin
    // contenido; esto es un apagador explícito adicional, independiente del contenido.
    IReadOnlyDictionary<string, bool>? SectionVisibility = null,
    // Solo fotos (URLs), sin caption — máximo 12.
    IReadOnlyList<string>? GalleryImages = null
);

public record FeaturedAffiliateDto(
    string Slug,
    string Name,
    string? Description,
    string? LogoUrl
);

public record CatalogItemDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string? Category,
    string? ImageUrl,
    int SortOrder,
    bool IsDemo,
    int? DurationMinutes,   // Service/Barber only
    int? Stock,             // InventoryItem only (null = no tracking)
    string? Status,
    string? DescriptionEn = null,               // Product/Service/InventoryItem
    IReadOnlyList<string>? Periods = null,      // Product only — breakfast/lunch/dinner/late_night/all_day
    IReadOnlyList<string>? WeekDays = null,     // Product only — monday..sunday
    IReadOnlyList<string>? Flags = null,        // Product only — free tokens, e.g. vegetarian/spicy/glutenFree
    bool? Featured = null,                      // Product only
    bool? Popular = null,                       // Product only
    string? VideoUrl = null,                    // Product only — Menu Board Fase 9 Etapa A
    IReadOnlyList<string>? Images = null,       // Product/Service/InventoryItem — galería completa; Images[0] == ImageUrl
    string? NameEn = null,                      // Product/Service/InventoryItem — nombre en inglés, fallback a Name si null
    IReadOnlyList<PublicIngredientDto>? Ingredients = null  // Product (Restaurante) — solo si tiene receta; null/vacío = sin receta definida
);

// Solo nombre — a propósito no expone Quantity (cantidad por porción, dato interno de receta)
// ni nada de InventoryItem (stock, costo). El kiosko/tienda pública solo necesita saber qué
// contiene el plato para mostrarlo y dejar que el cliente lo quite de su pedido si aplica.
public record PublicIngredientDto(Guid InventoryItemId, string Name);

public record PlanCapabilitiesDto(
    bool OnlinePayments,
    bool BookingCalendar,
    bool MenuModifiers,
    bool RealtimeStock,
    bool BrandingFull,
    bool HidePoweredBy,
    bool CustomDomain,
    bool MenuBoard
);

public record PublicCatalogResponse(
    AffiliatePublicDto Affiliate,
    List<CatalogItemDto> Items,
    PlanCapabilitiesDto Capabilities,
    List<ScreenAdDto>? ScreenAds = null,   // Fase 9 Etapa A — solo tiene contenido si hay comerciales activos vigentes
    int? AdFrequency = null,               // cada cuántos slides de categoría se inserta un comercial (null/0 = ninguno)
    string? Language = null,               // Fase 9 — "es" | "en", preferencia del board
    string? BoardTheme = null,             // Fase 9 — "Dark" | "Light"
    string? TransitionEffect = null        // Fase 9 — "Fade" | "Slide" | "Zoom" | "None"
);
