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

    /// <summary>
    /// Publica/despublica o suspende/reactiva un negocio desde /ops. Published es la preferencia
    /// del dueño (misma bandera que usa el dashboard normal); IsActive es un override de
    /// plataforma independiente — "tumbar temporalmente" sin tocar su config real, así que al
    /// reactivar vuelve exactamente como estaba. Null en cualquiera de los dos = no lo toques.
    /// </summary>
    Task<PlatformAffiliateSummaryDto> SetAffiliateStatusAsync(Guid affiliateId, bool? published, bool? active);

    Task<ImpersonationSessionDto> StartImpersonationAsync(string adminSupabaseUserId, string adminEmail, Guid affiliateId);
    Task EndImpersonationAsync(string adminSupabaseUserId);
}
