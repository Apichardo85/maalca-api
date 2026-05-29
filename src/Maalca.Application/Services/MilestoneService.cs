using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class MilestoneService : IMilestoneService
{
    private readonly AppDbContext _db;

    public MilestoneService(AppDbContext db) => _db = db;

    public async Task<HashSet<string>> GetCompletedKeysAsync(Guid affiliateId)
    {
        var keys = await _db.AffiliateMilestones
            .Where(m => m.AffiliateId == affiliateId)
            .Select(m => m.Key)
            .ToListAsync();
        return new HashSet<string>(keys);
    }

    public async Task MarkAsync(Guid affiliateId, string key, string? metadata = null)
    {
        var exists = await _db.AffiliateMilestones
            .AnyAsync(m => m.AffiliateId == affiliateId && m.Key == key);
        if (exists) return;

        try
        {
            _db.AffiliateMilestones.Add(new AffiliateMilestone
            {
                AffiliateId = affiliateId,
                Key = key,
                Metadata = metadata
            });
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Race condition: concurrent request already inserted. Idempotent — swallow.
        }
    }
}
