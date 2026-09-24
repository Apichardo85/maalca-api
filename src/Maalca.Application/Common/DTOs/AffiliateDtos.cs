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
    string? ZoomLink = null    // Link fijo de la sala de Zoom del negocio (ver Affiliate.ZoomLink)
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
    string? ZoomLink = null
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
    // Comunidad — reemplazo total de la lista (igual que ProcessSteps/Faq), no CRUD por fila.
    List<CausaDto>? Causas = null,
    // Comunidad — reemplazo total del objeto. null = no se toca; un objeto con campos null
    // adentro SÍ actualiza (permite borrar un dato ya guardado, ej. quitar la meta del mes).
    CommunityImpactDto? CommunityImpact = null
);

public record AffiliateContentDto(
    IReadOnlyList<ProcessStepDto> ProcessSteps,
    IReadOnlyList<FaqItemDto> Faq,
    IReadOnlyList<HorarioEntryDto> Horario,
    IReadOnlyDictionary<string, bool> SectionVisibility,
    IReadOnlyList<string> GalleryImages,
    IReadOnlyList<CausaDto> Causas,
    CommunityImpactDto? CommunityImpact
);

// ── Comunidad — causas y punto de entrega/meta (ver Affiliate.Causas / Affiliate.CommunityImpact) ──

// Id lo genera el frontend (crypto.randomUUID()) solo para tener key de React estable entre
// guardados — el backend no lo valida como único ni lo usa para nada, es reemplazo total de
// la lista en cada PATCH, igual que ProcessSteps/Faq.
public record CausaDto(
    string Id,
    string Title,
    // "money" | "time" | "in_kind" — validado en AffiliateService.
    string Type,
    string? Description,
    // Solo aplica/se muestra si Type == "money". GoalAmount null = sin meta (se puede
    // reportar solo lo recaudado sin una meta fija).
    decimal? GoalAmount,
    decimal? CurrentAmount
);

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
