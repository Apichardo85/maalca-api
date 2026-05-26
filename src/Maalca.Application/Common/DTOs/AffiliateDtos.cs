namespace Maalca.Application.Common.DTOs;

public record AffiliateSummaryDto(
    Guid AffiliateId,
    string Name,
    string? Slug,         // null — Fase B
    string? BusinessType, // null — Fase B
    string? Plan,         // null — Fase B
    string Role
);
