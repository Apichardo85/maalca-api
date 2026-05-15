using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class AffiliateMapService : IAffiliateMapService
{
    private readonly AppDbContext _context;

    public AffiliateMapService(AppDbContext context) => _context = context;

    public async Task<List<UserAffiliateMap>> GetMapsForUserAsync(string supabaseUserId)
        => await _context.UserAffiliateMaps
            .Where(m => m.SupabaseUserId == supabaseUserId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

    public async Task<UserAffiliateMap?> GetMapAsync(string supabaseUserId, Guid affiliateId)
        => await _context.UserAffiliateMaps
            .FirstOrDefaultAsync(m => m.SupabaseUserId == supabaseUserId
                                   && m.AffiliateId == affiliateId);

    public async Task<UserAffiliateMap> CreateMapAsync(string supabaseUserId, string email,
                                                        Guid affiliateId, AffiliateRole role)
    {
        var map = new UserAffiliateMap
        {
            SupabaseUserId = supabaseUserId,
            Email = email,
            AffiliateId = affiliateId,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        _context.UserAffiliateMaps.Add(map);
        await _context.SaveChangesAsync();
        return map;
    }
}
