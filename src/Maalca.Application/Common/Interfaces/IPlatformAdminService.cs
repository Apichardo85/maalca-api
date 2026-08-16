using Maalca.Application.Common.DTOs;
using Maalca.Domain.Entities;

namespace Maalca.Application.Common.Interfaces;

public interface IPlatformAdminService
{
    /// <summary>
    /// Comprueba (y reclama, si aplica) si este usuario es admin de plataforma. Mismo patrón
    /// invite-claim que ClaimPendingInvitesAsync: si hay una fila sembrada con SupabaseUserId=""
    /// y el email coincide, la engancha a este usuario en la misma llamada.
    /// </summary>
    Task<bool> IsPlatformAdminAsync(string supabaseUserId, string email);

    /// <summary>
    /// Rol del admin ya reclamado (asume que IsPlatformAdminAsync ya corrió esta request y, si
    /// aplicaba, ya hizo el claim). Devuelve null si no es admin.
    /// </summary>
    Task<PlatformAdminRole?> GetRoleAsync(string supabaseUserId);

    Task<PlatformOpsOverviewDto> GetOverviewAsync();
    Task<List<PlatformAffiliateSummaryDto>> GetAffiliatesAsync();

    /// <summary>
    /// Publica/despublica o suspende/reactiva un negocio desde /ops. Published es la preferencia
    /// del dueño (misma bandera que usa el dashboard normal); IsActive es un override de
    /// plataforma independiente — "tumbar temporalmente" sin tocar su config real, así que al
    /// reactivar vuelve exactamente como estaba. Null en cualquiera de los dos = no lo toques.
    /// </summary>
    Task<PlatformAffiliateSummaryDto> SetAffiliateStatusAsync(Guid affiliateId, bool? published, bool? active);

    /// <summary>Control de módulos por afiliado — MaalCa prende/apaga tokens del whitelist por
    /// encima de lo que el plan normalmente daría.</summary>
    Task<PlatformAffiliateSummaryDto> SetAffiliateModulesAsync(Guid affiliateId, List<string> modules);

    Task<ImpersonationSessionDto> StartImpersonationAsync(string adminSupabaseUserId, string adminEmail, Guid affiliateId);
    Task EndImpersonationAsync(string adminSupabaseUserId);

    // ---- Equipo interno de plataforma (solo Owner puede invitar/cambiar rol/quitar) ----
    Task<List<PlatformTeamMemberDto>> GetPlatformTeamAsync();
    Task<PlatformTeamMemberDto> InvitePlatformAdminAsync(string email, PlatformAdminRole role);
    Task<PlatformTeamMemberDto> UpdatePlatformAdminRoleAsync(Guid platformAdminId, PlatformAdminRole role);
    Task RemovePlatformAdminAsync(Guid platformAdminId);

    // ---- Notas CRM internas por afiliado ----
    Task<List<AffiliateNoteDto>> GetAffiliateNotesAsync(Guid affiliateId);
    Task<AffiliateNoteDto> AddAffiliateNoteAsync(Guid affiliateId, string authorEmail, string text);
}
