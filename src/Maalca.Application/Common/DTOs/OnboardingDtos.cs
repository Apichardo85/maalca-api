namespace Maalca.Application.Common.DTOs;

public record OnboardingRequest(
    string Name,
    string BusinessType,
    string? WhatsApp = null,
    string? Description = null
);

public record OnboardingResponse(
    Guid AffiliateId,
    string Name,
    string Slug,
    string BusinessType,
    string? Description = null,
    string? WhatsApp = null
);
