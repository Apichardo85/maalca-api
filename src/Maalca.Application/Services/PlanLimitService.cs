using Maalca.Application.Common;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class PlanLimitService : IPlanLimitService
{
    public const string TrialExpiredMessage = "Tu período gratuito terminó — mejora a Emprendedor para seguir editando.";

    private readonly AppDbContext _db;

    public PlanLimitService(AppDbContext db)
    {
        _db = db;
    }

    // Una sola definición del trial: ModuleAccess (el override manual desde /ops,
    // Affiliate.TrialOverrideEndsAt, tiene prioridad sobre CreatedAt + 30 días).
    public bool IsTrialExpired(Affiliate affiliate) => ModuleAccess.IsTrialExpired(affiliate);

    public int GetMaxItems(Plan plan) => plan switch
    {
        Plan.Entrepreneur or Plan.Enterprise => int.MaxValue,
        _ => 10
    };

    // Durante el trial activo (Free, sin vencer) no hay tope de items — el trial debe sentirse
    // como el plan pagado. Al vencer el trial cae al tope normal de Free (10), en vez del bloqueo
    // total que existía antes.
    public int GetMaxItems(Affiliate affiliate)
    {
        if (affiliate.Plan == Plan.Free && !IsTrialExpired(affiliate))
            return int.MaxValue;

        return GetMaxItems(affiliate.Plan);
    }

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

    // ── Pantallas (Menu Board / muros) ──────────────────────────────────
    // Por plan de pago (para cotizar en Stripe): Growth 1 · Professional 2.
    public int GetIncludedScreens(Plan plan) => plan switch
    {
        Plan.Enterprise => 2,
        _ => 1
    };

    // Las que tiene HOY el negocio: trial vigente 1 · Starter vencido 0 (salvo cortesía) ·
    // Growth 1 · Professional 2 (ModuleAccess.GetIncludedScreens), más las extra pagadas.
    public int GetScreenAllowance(Affiliate affiliate)
        => ModuleAccess.GetIncludedScreens(affiliate) + Math.Max(0, affiliate.ExtraScreens);

    public async Task EnsureScreenCapacityAsync(Guid affiliateId, int adding)
    {
        if (adding <= 0) return;
        var affiliate = await _db.Affiliates.FindAsync(affiliateId)
            ?? throw new KeyNotFoundException("Affiliate not found");
        var used = await _db.Screens.CountAsync(s => s.AffiliateId == affiliateId);
        var allowance = GetScreenAllowance(affiliate);
        if (used + adding > allowance)
            throw new ScreenLimitException(allowance, used, adding);
    }

    public async Task<bool> CanAddItemAsync(Guid affiliateId)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId);
        if (affiliate == null) return false;

        var current = await GetCurrentItemCountAsync(affiliateId);
        return current < GetMaxItems(affiliate);
    }
}
