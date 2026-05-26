using Maalca.Domain.Entities;

namespace Maalca.Application.Common.Interfaces;

public interface IAffiliateMapService
{
    Task<List<UserAffiliateMap>> GetMapsForUserAsync(string supabaseUserId);
    Task<UserAffiliateMap?> GetMapAsync(string supabaseUserId, Guid affiliateId);
    Task<UserAffiliateMap> CreateMapAsync(string supabaseUserId, string email,
                                          Guid affiliateId, AffiliateRole role);
}
