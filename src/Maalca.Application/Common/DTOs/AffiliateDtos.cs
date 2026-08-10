namespace Maalca.Application.Common.DTOs;

public record AffiliateSummaryDto(
    Guid AffiliateId,
    string Name,
    string? Slug,         // null — Fase B
    string? BusinessType, // null — Fase B
    string? Plan,         // null — Fase B
    string Role
);

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
    int? AdFrequency = null,  // Fase 9 Etapa A — cada cuántos slides de menú se inserta un comercial en el board
    string? Language = null,    // Fase 9 — "es" | "en", preferencia del board (no del visitante)
    string? BoardTheme = null,  // Fase 9 — "Dark" | "Light"
    string? TransitionEffect = null   // Fase 9 — "Fade" | "Slide" | "Zoom" | "None"
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
    string? Country = null
);

public record UpdateAffiliateContentRequest(
    List<ProcessStepDto>? ProcessSteps,
    List<FaqItemDto>? Faq,
    List<HorarioEntryDto>? Horario
);

public record AffiliateContentDto(
    IReadOnlyList<ProcessStepDto> ProcessSteps,
    IReadOnlyList<FaqItemDto> Faq,
    IReadOnlyList<HorarioEntryDto> Horario
);

public record AffiliateEventRequest(
    string Type,
    Dictionary<string, string>? Metadata = null
);

public record AffiliateSlugLookupDto(Guid Id, string Slug, string Name);
