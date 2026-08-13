using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IPlatformAdminService
{
    /// <summary>
    /// Comprueba (y reclama, si aplica) si este usuario es admin de plataforma. Mismo patrón
    /// invite-claim que ClaimPendingInvitesAsync: si hay una fila sembrada con SupabaseUserId=""
    /// y el email coincide, la engancha a este usuario en la misma llamada.
    /// </summary>
    Task<bool> IsPlatformAdminAsync(string supabaseUserId, string email);

    Task<PlatformOpsOverviewDto> GetOverviewAsync();
    Task<List<PlatformAffiliateSummaryDto>> GetAffiliatesAsync();

    Task<ImpersonationSessionDto> StartImpersonationAsync(string adminSupabaseUserId, string adminEmail, Guid affiliateId);
    Task EndImpersonationAsync(string adminSupabaseUserId);
}
