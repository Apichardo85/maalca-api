namespace Maalca.Application.Common.DTOs;

public record OnboardingRequest(
    string Name,
    string BusinessType,
    string? WhatsApp = null,
    string? Description = null,
    string? PrimaryColor = null,
    string? LogoUrl = null,
    // "Individual" | "Organization" — solo relevante para BusinessType=Community (ver
    // Affiliate.OperatorType). Omitido = Organization.
    string? OperatorType = null,
    // true = modo "Solo pantallas" (ver Affiliate.ScreensOnly): sin catálogo demo, página
    // apagada, módulos board/catalog/page y sin setup al pagar.
    bool? ScreensOnly = null
);

public record OnboardingResponse(
    Guid AffiliateId,
    string Name,
    string Slug,
    string BusinessType,
    string? Description = null,
    string? WhatsApp = null,
    string? PrimaryColor = null,
    string? LogoUrl = null
);
