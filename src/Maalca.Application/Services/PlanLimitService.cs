using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class PlanLimitService : IPlanLimitService
{
    private readonly AppDbContext _db;

    public PlanLimitService(AppDbContext db)
    {
        _db = db;
    }

    public int GetMaxItems(Plan plan) => plan switch
    {
        Plan.Entrepreneur => int.MaxValue,
        _ => 10
    };

    public async Task<int> GetCurrentItemCountAsync(Guid affiliateId)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId);
        if (affiliate == null) return 0;

        return affiliate.BusinessType switch
        {
            BusinessType.Restaurant or BusinessType.Creator or BusinessType.Publisher =>
                await _db.Products.CountAsync(p => p.AffiliateId == affiliateId && !p.IsDemo),

            BusinessType.Barber or BusinessType.Service or BusinessType.Professional =>
                await _db.Services.CountAsync(s => s.AffiliateId == affiliateId && !s.IsDemo),

            BusinessType.Retail =>
                await _db.InventoryItems.CountAsync(i => i.AffiliateId == affiliateId && !i.IsDemo),

            _ => 0
        };
    }

    public async Task<bool> CanAddItemAsync(Guid affiliateId)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId);
        if (affiliate == null) return false;

        var current = await GetCurrentItemCountAsync(affiliateId);
        return current < GetMaxItems(affiliate.Plan);
    }
}
