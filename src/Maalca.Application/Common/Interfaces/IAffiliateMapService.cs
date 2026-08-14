using Maalca.Domain.Entities;

namespace Maalca.Application.Common.Interfaces;

public interface IAffiliateMapService
{
    Task<List<UserAffiliateMap>> GetMapsForUserAsync(string supabaseUserId);
    Task<UserAffiliateMap?> GetMapAsync(string supabaseUserId, Guid affiliateId);
    Task<UserAffiliateMap> CreateMapAsync(string supabaseUserId, string email,
                                          Guid affiliateId, AffiliateRole role);

    /// Al iniciar sesión (llamado desde GET /api/me/affiliates), engancha cualquier invitación
    /// pendiente (SupabaseUserId vacío) que coincida con el email verificado de esta sesión —
    /// así una persona invitada por su dueño solo necesita crear su cuenta con ese mismo email
    /// y automáticamente ve el negocio la próxima vez que entra, sin flujo de aceptación aparte.
    Task ClaimPendingInvitesAsync(string supabaseUserId, string email);

    Task<List<UserAffiliateMap>> GetTeamAsync(Guid affiliateId);
    Task<UserAffiliateMap> InviteAsync(Guid affiliateId, string email, AffiliateRole role, Guid? teamMemberId = null);
    Task<UserAffiliateMap?> UpdateRoleAsync(Guid affiliateId, Guid mapId, AffiliateRole role);
    Task<bool> RemoveAsync(Guid affiliateId, Guid mapId);
}
