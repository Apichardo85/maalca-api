using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class PublicCatalogService : IPublicCatalogService
{
    private readonly AppDbContext _db;

    public PublicCatalogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AffiliatePublicDto?> GetAffiliateBySlugAsync(string slug)
    {
        var affiliate = await _db.Affiliates
            .Where(a => a.Slug == slug && a.Published)
            .FirstOrDefaultAsync();

        if (affiliate == null) return null;

        return await MapToAffiliatePublicDtoAsync(affiliate);
    }

    public async Task<PublicCatalogResponse?> GetCatalogAsync(string slug)
    {
        var affiliate = await _db.Affiliates
            .Where(a => a.Slug == slug && a.Published)
            .FirstOrDefaultAsync();

        if (affiliate == null) return null;

        var items = affiliate.BusinessType switch
        {
            BusinessType.Restaurant or BusinessType.Creator or BusinessType.Publisher =>
                await _db.Products
                    .Where(p => p.AffiliateId == affiliate.Id && p.IsPubliclyVisible)
                    .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
                    .Select(p => new CatalogItemDto(
                        p.Id, p.Name, p.Description, p.Price,
                        p.Category, p.ImageUrl, p.SortOrder, p.IsDemo,
                        null, null, p.Status))
                    .ToListAsync(),

            BusinessType.Barber or BusinessType.Service or BusinessType.Professional =>
                await _db.Services
                    .Where(s => s.AffiliateId == affiliate.Id && s.IsPubliclyVisible)
                    .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
                    .Select(s => new CatalogItemDto(
                        s.Id, s.Name, s.Description, s.Price,
                        s.Category, s.ImageUrl, s.SortOrder, s.IsDemo,
                        s.DurationMinutes, null, s.Status))
                    .ToListAsync(),

            BusinessType.Retail =>
                await _db.InventoryItems
                    .Where(i => i.AffiliateId == affiliate.Id && i.IsPubliclyVisible)
                    .OrderBy(i => i.SortOrder).ThenBy(i => i.Name)
                    .Select(i => new CatalogItemDto(
                        i.Id, i.Name, i.Description, i.UnitPrice,
                        i.Category, i.ImageUrl, i.SortOrder, i.IsDemo,
                        null, i.Quantity, i.Status))
                    .ToListAsync(),

            _ => new List<CatalogItemDto>()
        };

        return new PublicCatalogResponse(
            await MapToAffiliatePublicDtoAsync(affiliate),
            items,
            BuildCapabilities(affiliate.Plan));
    }

    private async Task<AffiliatePublicDto> MapToAffiliatePublicDtoAsync(Maalca.Domain.Entities.Affiliate a)
    {
        var canales = (await _db.Canales
            .Where(c => c.AffiliateId == a.Id && c.Activo)
            .OrderBy(c => c.Orden)
            .ToListAsync())
            .Select(c => new CanalDto(c.Id, c.Tipo.ToString(), c.Metodo.ToString(), c.ValorCrudo,
                c.EnlaceGenerado, c.NombreVisible, c.Verificado, c.Orden, c.Activo))
            .ToList();

        return new(a.Id, a.Name, a.Slug!, a.BusinessType.ToString(),
            a.Description, a.PrimaryColor, a.LogoUrl, a.CoverImageUrl,
            a.WhatsApp, a.ContactEmail, a.Address,
            null,   // City — Affiliate entity does not have this field yet
            a.Website,
            canales);
    }

    private static PlanCapabilitiesDto BuildCapabilities(Plan plan) =>
        plan == Plan.Entrepreneur
            ? new PlanCapabilitiesDto(true, true, true, true, true, true, true)
            : new PlanCapabilitiesDto(false, false, false, false, false, false, false);
}
