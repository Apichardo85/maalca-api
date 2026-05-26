namespace Maalca.Application.Common.DTOs;

public record OnboardingRequest(
    string Name,
    string BusinessType
);

public record OnboardingResponse(
    Guid AffiliateId,
    string Name,
    string Slug,
    string BusinessType
);
