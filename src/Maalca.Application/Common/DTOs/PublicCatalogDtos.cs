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
    // Link fijo de la sala de Zoom del negocio — ver comentario en Affiliate.ZoomLink. Se
    // muestra al cliente cuando reserva un servicio Virtual/Both y elige modalidad virtual.
    string? ZoomLink,
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
    IReadOnlyList<string>? GalleryImages = null,
    CommunityImpactDto? CommunityImpact = null,
    // Paleta del negocio (null = la plantilla deriva los neutros del primario).
    string? SecondaryColor = null,
    string? AccentColor = null,
    IReadOnlyDictionary<string, MealPeriodRangeDto>? MealPeriodHours = null,
    IReadOnlyDictionary<string, CategoryTranslationDto>? CategoryTranslations = null,
    // Tema por defecto de la vitrina ("dark" | "light"; null = sigue la preferencia del visitante/sistema).
    // Sale de Affiliate.Settings -> "defaultTheme"; no requiere columna nueva.
    string? DefaultTheme = null,
    string? SpanishFlag = null,
    // Idioma principal del negocio (Affiliate.Language: "es" | "en"). Idioma inicial de la vitrina
    // cuando el visitante aun no eligio uno, y base de los correos al cliente.
    string? Language = null,
    // false = el módulo "orders" está apagado para este negocio (nadie está atendiendo pedidos en línea):
    // la vitrina oculta el carrito/pickup y ofrece el canal directo (WhatsApp/teléfono). Default true
    // para no cambiar nada en clientes que no usan el toggle.
    bool OrdersEnabled = true
    // Causas ya no viene acá -- tiene su propio endpoint público
    // /api/public/affiliates/{slug}/causas (backlog 2026-09-25, ver Causa.cs).
);

public record FeaturedAffiliateDto(
    string Slug,
    string Name,
    string? Description,
    string? DescriptionEn,
    string? LogoUrl,
    string? CoverImageUrl,
    string BusinessType,
    string? PrimaryColor,
    string? SecondaryColor,
    string? AccentColor
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
    IReadOnlyList<PublicIngredientDto>? Ingredients = null, // Product (Restaurante) — solo si tiene receta; null/vacío = sin receta definida
    string? Modality = null,                    // Service only — "InPerson" | "Virtual" | "Both". Null en Product/InventoryItem.
    IReadOnlyList<PublicModifierGroupDto>? ModifierGroups = null, // Product (Restaurante) — grupos reutilizables enlazados (ej. "Guarnición"); null/vacío = sin modificadores
    decimal? CompareAtPrice = null,             // InventoryItem only — precio "antes" tachado (oferta); null = sin oferta
    bool? TrackStock = null                     // InventoryItem only — true = Stock es real (mostrar Agotado/Quedan N); null/false = sin control
);

// Solo nombre — a propósito no expone Quantity (cantidad por porción, dato interno de receta)
// ni nada de InventoryItem (stock, costo). El kiosko/tienda pública solo necesita saber qué
// contiene el plato para mostrarlo y dejar que el cliente lo quite de su pedido si aplica.
public record PublicIngredientDto(Guid InventoryItemId, string Name);

// Vista pública de un ModifierGroup/ModifierOption — mismo shape que el dashboard (ModifierGroupDto
// en CatalogCrudDtos.cs) pero sin campos internos de administración (SortOrder de edición, etc.
// se mantiene porque el cliente sí necesita el orden de despliegue).
public record PublicModifierOptionDto(Guid Id, string Name, decimal PriceDelta, bool IsDefault);

public record PublicModifierGroupDto(
    Guid Id,
    string Name,
    int MinSelect,
    int MaxSelect,
    bool Required,
    IReadOnlyList<PublicModifierOptionDto> Options
);

public record PlanCapabilitiesDto(
    bool OnlinePayments,
    bool BookingCalendar,
    bool MenuModifiers,
    bool RealtimeStock,
    bool BrandingFull,
    bool HidePoweredBy,
    bool CustomDomain,
    bool MenuBoard,
    // true = el afiliado completó Stripe Connect (ChargesEnabled): solo entonces la web ofrece pago con tarjeta.
    bool StripeReady = false,
    // Regla de confianza: un cliente nuevo paga al recoger solo hasta este total (null = sin límite).
    // El checkout lo muestra como aviso; el API lo hace cumplir al crear el pedido.
    decimal? PickupNewCustomerLimit = null
);

public record PublicCatalogResponse(
    AffiliatePublicDto Affiliate,
    List<CatalogItemDto> Items,
    PlanCapabilitiesDto Capabilities,
    List<ScreenAdDto>? ScreenAds = null,   // Fase 9 Etapa A — solo tiene contenido si hay comerciales activos vigentes
    int? AdFrequency = null,               // cada cuántos slides de categoría se inserta un comercial (null/0 = ninguno)
    string? Language = null,               // Fase 9 — "es" | "en", preferencia del board
    string? BoardTheme = null,             // Fase 9 — "Dark" | "Light"
    string? TransitionEffect = null,        // Fase 9 — "Fade" | "Slide" | "Zoom" | "None"
    string? Layout = null,                 // "Classic" | "Hero" | "Grid" | ... plantilla visual
    PublicWallDto? Wall = null,            // muro sincronizado: modo + posición de ESTA TV (null = pantalla suelta)
    // false = página pública apagada (negocio "Solo pantallas" sin publicar): el board sí usa
    // este catálogo, pero la página /{slug}, el kiosko y el detalle de item deben dar 404.
    bool PagePublished = true
);
