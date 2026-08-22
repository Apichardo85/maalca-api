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
    List<string> Alerts,
    string? LogoUrl = null,
    /// Módulos efectivamente activos (ya filtrados por ModuleCatalog.FilterActive) — la misma
    /// lista que ve el afiliado en /space/{slug}/modules, expuesta acá para que /ops pueda
    /// mostrar y editar el toggle real.
    List<string>? ModulosActivos = null);

/// <summary>Control de módulos por afiliado (Etapa: MaalCa converge plan + overrides) — MaalCa
/// puede prender/apagar cualquier token del whitelist por encima de lo que el plan normalmente
/// daría. Lista vacía = explícitamente ningún módulo (distinto de nunca haberlo configurado).</summary>
public record SetAffiliateModulesRequest(List<string> Modules);

public record ImpersonationSessionDto(Guid AffiliateId, string Slug, string Name, DateTime ExpiresAt);

public record SetAffiliateStatusRequest(bool? Published, bool? Active);

/// <summary>Cambio manual de tier desde /ops — MaalCa puede mover un negocio de Gratis a
/// Emprendedor/Enterprise (o al revés) sin pasar por Stripe, ej. cortesía, negociación directa,
/// o corregir un caso donde el pago no sincronizó. Acción de Owner únicamente (igual que
/// publicar/suspender) por su impacto financiero.</summary>
public record SetAffiliatePlanRequest(string Plan);

/// <summary>Info del propio admin autenticado — reemplaza el antiguo { isPlatformAdmin: bool } de /api/me/admin-status.</summary>
public record MyAdminStatusDto(bool IsPlatformAdmin, string? Role);

public record PlatformTeamMemberDto(Guid Id, string Email, string Role, bool Pending, DateTime CreatedAt);

public record InvitePlatformAdminRequest(string Email, string Role);

public record UpdatePlatformAdminRoleRequest(string Role);

public record AffiliateNoteDto(Guid Id, string AuthorEmail, string Text, DateTime CreatedAt);

public record CreateAffiliateNoteRequest(string Text);
