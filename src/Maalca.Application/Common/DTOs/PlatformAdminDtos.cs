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
    bool IsActive,
    DateTime CreatedAt,
    int OrdersLast30Days,
    bool StripeConnectChargesEnabled,
    List<string> Alerts);

public record ImpersonationSessionDto(Guid AffiliateId, string Slug, string Name, DateTime ExpiresAt);

public record SetAffiliateStatusRequest(bool? Published, bool? Active);

/// <summary>Info del propio admin autenticado — reemplaza el antiguo { isPlatformAdmin: bool } de /api/me/admin-status.</summary>
public record MyAdminStatusDto(bool IsPlatformAdmin, string? Role);

public record PlatformTeamMemberDto(Guid Id, string Email, string Role, bool Pending, DateTime CreatedAt);

public record InvitePlatformAdminRequest(string Email, string Role);

public record UpdatePlatformAdminRoleRequest(string Role);

public record AffiliateNoteDto(Guid Id, string AuthorEmail, string Text, DateTime CreatedAt);

public record CreateAffiliateNoteRequest(string Text);
