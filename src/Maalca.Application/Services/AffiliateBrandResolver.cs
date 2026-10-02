using Maalca.Application.Common.Interfaces;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class AffiliateBrandResolver : IAffiliateBrandResolver
{
    private readonly AppDbContext _db;
    public AffiliateBrandResolver(AppDbContext db) => _db = db;

    public async Task<AffiliateBrand> GetAsync(Guid affiliateId)
    {
        try
        {
            var a = await _db.Affiliates.AsNoTracking()
                .Where(x => x.Id == affiliateId)
                .Select(x => new { x.LogoUrl, x.Logo, x.PrimaryColor, x.Slug })
                .FirstOrDefaultAsync();
            if (a == null) return new AffiliateBrand(null, null, null);
            return new AffiliateBrand(string.IsNullOrWhiteSpace(a.LogoUrl) ? a.Logo : a.LogoUrl, a.PrimaryColor, a.Slug);
        }
        catch
        {
            return new AffiliateBrand(null, null, null);
        }
    }
}
