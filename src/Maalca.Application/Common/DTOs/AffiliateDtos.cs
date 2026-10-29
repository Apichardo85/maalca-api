namespace Maalca.Application.Common.DTOs;

public record AffiliateSummaryDto(
    Guid AffiliateId,
    string Name,
    string? Slug,         // null — Fase B
    string? BusinessType, // null — Fase B
    string? Plan,         // null — Fase B
    string Role
);

// Dashboard multiusuario con roles — Pending = true cuando el dueño invitó por email pero esa
// persona todavía no inició sesión ninguna vez (SupabaseUserId sigue vacío hasta que lo hace).
public record TeamMemberDto(
    Guid Id,
    string Email,
    string Role,
    bool Pending,
    DateTime CreatedAt,
    Guid? TeamMemberId = null
);

public record InviteTeamMemberRequest(string Email, string Role, Guid? TeamMemberId = null);

public record UpdateTeamMemberRoleRequest(string Role);

public record UpdateAffiliateProfileRequest(
    string? Name,
    string? Description,
    string? DescriptionEn,
    string? LogoUrl,
    string? CoverImageUrl,
    string? ContactEmail,
    string? Address,
    string? Website,
    string? PrimaryColor,
    string? Country = null,
    string? Currency = null,   // "USD" | "DOP"
    int? AdFrequency = null,  // Fase 9 Etapa A — cada cuántos slides de menú se inserta un comercial en el board
    string? Language = null,    // Fase 9 — "es" | "en", preferencia del board (no del visitante)
    string? BoardTheme = null,  // Fase 9 — "Dark" | "Light"
    string? TransitionEffect = null,  // Fase 9 — "Fade" | "Slide" | "Zoom" | "None"
    string? ZoomLink = null,   // Link fijo de la sala de Zoom del negocio (ver Affiliate.ZoomLink)
    // Paleta (ver Affiliate.SecondaryColor/AccentColor): "#RRGGBB"; "" = borrar y volver al cálculo automático.
    string? SecondaryColor = null,
    string? AccentColor = null,
    string? BoardLayout = null,  // "Classic" | "Hero" | "Grid" | ... (ver BoardLayout enum)
    string? BoardOrientation = null,  // "Landscape" | "Portrait" (solo cuenta con BoardLayout = Panels)
    List<BoardPanelDto>? BoardPanels = null  // 1-3 flyers; [] = volver al default
);

public record AffiliatePublicProfileDto(
    Guid Id,
    string Name,
    string Slug,
    string BusinessType,
    string Plan,
    string? Description,
    string? DescriptionEn,
    string? PrimaryColor,
    string? LogoUrl,
    string? CoverImageUrl,
    string? ContactEmail,
    string? Address,
    string? Website,
    string? Country = null,
    string? Currency = null,
    string? ZoomLink = null,
    string? SecondaryColor = null,
    string? AccentColor = null
);

public record UpdateAffiliateContentRequest(
    List<ProcessStepDto>? ProcessSteps,
    List<FaqItemDto>? Faq,
    List<HorarioEntryDto>? Horario,
    // Clave ausente = visible (default true) — solo se manda cuando el dueño explícitamente
    // prende/apaga una sección, nunca un objeto completo reconstruido desde cero.
    Dictionary<string, bool>? SectionVisibility = null,
    // Solo fotos (URLs), sin caption — máximo 12, validado en AffiliateService.
    List<string>? GalleryImages = null,
    // Comunidad — reemplazo total del objeto. null = no se toca; un objeto con campos null
    // adentro SÍ actualiza (permite borrar un dato ya guardado, ej. quitar la meta del mes).
    CommunityImpactDto? CommunityImpact = null,
    // Horarios de los momentos de comida (clave = breakfast/lunch/dinner/late_night). Reemplazo
    // total del objeto; null = no se toca; {} = borrar y volver a los cortes por defecto.
    Dictionary<string, MealPeriodRangeDto>? MealPeriodHours = null,
    // Traducción de los nombres de categoría (clave = nombre guardado). Reemplazo total;
    // null = no se toca; {} = borrar todas.
    Dictionary<string, CategoryTranslationDto>? CategoryTranslations = null,
    // Menú del día (rotación semanal). Reemplazo total; null = no se toca; Days vacío = borrar.
    DailyMenuDto? DailyMenu = null
    // Causas ya NO vive acá (backlog 2026-09-25) -- tiene su propio CRUD en
    // /api/affiliates/{id}/causas (ver ICausaService), igual que Activities. Se sacó del
    // reemplazo-total-del-array porque ya tenía datos reales en producción y necesitaba
    // poder editarse fila por fila.
);

public record AffiliateContentDto(
    IReadOnlyList<ProcessStepDto> ProcessSteps,
    IReadOnlyList<FaqItemDto> Faq,
    IReadOnlyList<HorarioEntryDto> Horario,
    IReadOnlyDictionary<string, bool> SectionVisibility,
    IReadOnlyList<string> GalleryImages,
    CommunityImpactDto? CommunityImpact,
    IReadOnlyDictionary<string, MealPeriodRangeDto>? MealPeriodHours = null,
    IReadOnlyDictionary<string, CategoryTranslationDto>? CategoryTranslations = null,
    DailyMenuDto? DailyMenu = null
);

/// <summary>
/// Menú del día: la rotación semanal que el restaurante anuncia en su flyer ("lunes: arroz blanco,
/// habichuelas rojas, pollo guisado..."). Days = clave monday..sunday → secciones en orden.
/// Note/NoteEn = línea corta sobre los combos ("incluye arroz, habichuelas y una guarnición").
/// </summary>
public record DailyMenuDto(
    string? Note,
    string? NoteEn,
    Dictionary<string, List<DailyMenuSectionDto>> Days
);

/// <summary>
/// Una sección del día. Kind:
///  - "List": lista simple sin precio (Arroz / Habichuelas / Guarniciones).
///  - "Combo": fila con platos + precio(s) del Product enlazado (si tiene un grupo de tamaños,
///    se muestran sus opciones: Pequeño/Mediano/Grande). Ej. Pollo / Cerdo / Res.
///  - "Featured": tarjeta destacada (Sopa del día, Desayuno) con el precio del Product enlazado.
/// From/To ("HH:mm", opcionales) = solo se muestra en ese horario (ej. desayuno 10:00–12:00).
/// El precio NUNCA se escribe acá: sale del producto, así un cambio de precio no deja el flyer viejo.
/// </summary>
public record DailyMenuSectionDto(
    string Name,
    string? NameEn,
    string Kind,
    List<string>? Items,
    List<string>? ItemsEn,
    Guid? ProductId = null,
    string? From = null,
    string? To = null
);

/// <summary>
/// Un flyer vertical del board (BoardLayout.Panels). Kind: "Daily" (menú del día), "Ads"
/// (comerciales a pantalla completa) o "Category" (los platos de una categoría, ej. Desayuno).
/// </summary>
public record BoardPanelDto(string Kind, string? Category = null);

/// <summary>Nombre de una categoría del catálogo en cada idioma (ambos opcionales).</summary>
public record CategoryTranslationDto(string? Es, string? En);

/// <summary>Rango "HH:mm" 24h de un momento de comida. end &lt; start = cruza medianoche.</summary>
public record MealPeriodRangeDto(string Start, string End);

// ── Comunidad — punto de entrega/meta (ver Affiliate.CommunityImpact). Causas tiene su
// propia entidad ahora (Causa.cs) -- ver comentario junto a UpdateAffiliateContentRequest. ──

public record CommunityImpactDto(
    // Reportado a mano por el afiliado — ver comentario en Affiliate.CommunityImpact.
    decimal? FundraisingGoalAmount,
    decimal? FundraisingCurrentAmount,
    string? DeliverySchedule,
    string? DeliveryAcceptedItems
);

public record AffiliateEventRequest(
    string Type,
    Dictionary<string, string>? Metadata = null
);

public record AffiliateSlugLookupDto(Guid Id, string Slug, string Name);
