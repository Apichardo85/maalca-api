using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class PlanLimitService : IPlanLimitService
{
    public const string TrialExpiredMessage = "Tu período gratuito terminó — mejora a Emprendedor para seguir editando.";
    private const int TrialDays = 30;

    private readonly AppDbContext _db;

    public PlanLimitService(AppDbContext db)
    {
        _db = db;
    }

    public bool IsTrialExpired(Affiliate affiliate) =>
        affiliate.Plan == Plan.Free && affiliate.CreatedAt.AddDays(TrialDays) < DateTime.UtcNow;

    public int GetMaxItems(Plan plan) => plan switch
    {
        Plan.Entrepreneur or Plan.Enterprise => int.MaxValue,
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
