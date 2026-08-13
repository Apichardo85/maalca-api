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
            .OrderByDescending(m => m.CreatedAt)
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

    public async Task ClaimPendingInvitesAsync(string supabaseUserId, string email)
    {
        if (string.IsNullOrEmpty(email)) return;

        var pending = await _context.UserAffiliateMaps
            .Where(m => m.SupabaseUserId == "" && m.Email.ToLower() == email.ToLower())
            .ToListAsync();
        if (pending.Count == 0) return;

        foreach (var map in pending)
            map.SupabaseUserId = supabaseUserId;
        await _context.SaveChangesAsync();
    }

    public async Task<List<UserAffiliateMap>> GetTeamAsync(Guid affiliateId)
        => await _context.UserAffiliateMaps
            .Where(m => m.AffiliateId == affiliateId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

    public async Task<UserAffiliateMap> InviteAsync(Guid affiliateId, string email, AffiliateRole role)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var exists = await _context.UserAffiliateMaps
            .AnyAsync(m => m.AffiliateId == affiliateId && m.Email.ToLower() == normalizedEmail);
        if (exists)
            throw new InvalidOperationException("Ese correo ya tiene acceso a este negocio.");

        // SupabaseUserId vacío = invitación pendiente, hasta que ClaimPendingInvitesAsync lo
        // enganche en el próximo login de esa persona con ese mismo correo verificado.
        var map = new UserAffiliateMap
        {
            SupabaseUserId = "",
            Email = normalizedEmail,
            AffiliateId = affiliateId,
            Role = role,
            CreatedAt = DateTime.UtcNow,
        };
        _context.UserAffiliateMaps.Add(map);
        await _context.SaveChangesAsync();
        return map;
    }

    public async Task<UserAffiliateMap?> UpdateRoleAsync(Guid affiliateId, Guid mapId, AffiliateRole role)
    {
        var map = await _context.UserAffiliateMaps
            .FirstOrDefaultAsync(m => m.Id == mapId && m.AffiliateId == affiliateId);
        if (map is null) return null;

        if (map.Role == AffiliateRole.Owner && role != AffiliateRole.Owner)
            await EnsureNotLastOwnerAsync(affiliateId, excludingMapId: mapId);

        map.Role = role;
        map.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return map;
    }

    public async Task<bool> RemoveAsync(Guid affiliateId, Guid mapId)
    {
        var map = await _context.UserAffiliateMaps
            .FirstOrDefaultAsync(m => m.Id == mapId && m.AffiliateId == affiliateId);
        if (map is null) return false;

        if (map.Role == AffiliateRole.Owner)
            await EnsureNotLastOwnerAsync(affiliateId, excludingMapId: mapId);

        _context.UserAffiliateMaps.Remove(map);
        await _context.SaveChangesAsync();
        return true;
    }

    // Un negocio siempre necesita al menos un Owner activo — si no, nadie podría volver a
    // gestionar el equipo ni recuperar el acceso.
    private async Task EnsureNotLastOwnerAsync(Guid affiliateId, Guid excludingMapId)
    {
        var otherOwners = await _context.UserAffiliateMaps
            .CountAsync(m => m.AffiliateId == affiliateId && m.Role == AffiliateRole.Owner && m.Id != excludingMapId);
        if (otherOwners == 0)
            throw new InvalidOperationException("No puedes quitar al último dueño del negocio.");
    }
}
