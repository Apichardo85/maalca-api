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
    // Comunidad — reemplazo total del objeto. null = no se toca; un objeto con campos null
    // adentro SÍ actualiza (permite borrar un dato ya guardado, ej. quitar la meta del mes).
    CommunityImpactDto? CommunityImpact = null
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
    CommunityImpactDto? CommunityImpact
);

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
