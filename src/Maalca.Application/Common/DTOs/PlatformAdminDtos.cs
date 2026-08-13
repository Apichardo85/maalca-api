namespace Maalca.Application.Common.DTOs;

public record PlatformOpsOverviewDto(
    int TotalAffiliates,
    int EntrepreneurCount,
    int FreeCount,
    decimal MrrUsd,
    int NewThisMonth,
    int PublishedCount);

public record PlatformAffiliateSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string BusinessType,
    string Plan,
    string PlanStatus,
    bool Published,
    DateTime CreatedAt,
    int OrdersLast30Days,
    bool StripeConnectChargesEnabled,
    List<string> Alerts);

public record ImpersonationSessionDto(Guid AffiliateId, string Slug, string Name, DateTime ExpiresAt);
