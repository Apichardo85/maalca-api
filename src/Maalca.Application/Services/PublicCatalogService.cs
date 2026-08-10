using Maalca.Application.Common;
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

    public async Task<PublicCatalogResponse?> GetCatalogAsync(string slug, Guid? screenId = null)
    {
        var affiliate = await _db.Affiliates
            .Where(a => a.Slug == slug && a.Published)
            .FirstOrDefaultAsync();

        if (affiliate == null) return null;

        // Fase 9 Etapa B — pantalla adicional (no la base /{slug}/board, que sigue siendo pura
        // preferencia de Affiliate). Si mandan un screenId que no existe o es de otro afiliado,
        // es un link roto/copiado mal — se trata igual que "afiliado no encontrado" (null → 404).
        Domain.Entities.Screen? screen = null;
        if (screenId.HasValue)
        {
            screen = await _db.Screens.FirstOrDefaultAsync(s => s.Id == screenId.Value && s.AffiliateId == affiliate.Id);
            if (screen == null) return null;
        }

        List<CatalogItemDto> items;
        if (affiliate.BusinessType is BusinessType.Restaurant or BusinessType.Creator or BusinessType.Publisher)
        {
            var products = await _db.Products
                .Where(p => p.AffiliateId == affiliate.Id && p.IsPubliclyVisible)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
                .ToListAsync();
            items = products.Select(CatalogItemMapper.FromProduct).ToList();
        }
        else
        {
            items = affiliate.BusinessType switch
            {
                BusinessType.Barber or BusinessType.Service or BusinessType.Professional =>
                    await _db.Services
                        .Where(s => s.AffiliateId == affiliate.Id && s.IsPubliclyVisible)
                        .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
                        .Select(s => new CatalogItemDto(
                            s.Id, s.Name, s.Description, s.Price,
                            s.Category, s.ImageUrl, s.SortOrder, s.IsDemo,
                            s.DurationMinutes, null, s.Status,
                            s.DescriptionEn, null, null, null, null, null, null))
                        .ToListAsync(),

                BusinessType.Retail =>
                    await _db.InventoryItems
                        .Where(i => i.AffiliateId == affiliate.Id && i.IsPubliclyVisible)
                        .OrderBy(i => i.SortOrder).ThenBy(i => i.Name)
                        .Select(i => new CatalogItemDto(
                            i.Id, i.Name, i.Description, i.UnitPrice,
                            i.Category, i.ImageUrl, i.SortOrder, i.IsDemo,
                            null, i.Quantity, i.Status,
                            i.DescriptionEn, null, null, null, null, null, null))
                        .ToListAsync(),

                _ => new List<CatalogItemDto>()
            };
        }

        // Fase 9 Etapa B — CategoryFilter de la pantalla (si la hay): recorta el catálogo a solo
        // esas categorías. Comparación case-insensitive porque el filtro se escribe a mano en el
        // dashboard y no hay validación contra las categorías reales del catálogo todavía.
        if (screen?.CategoryFilter is { Length: > 0 } categoryFilter)
        {
            var allowed = categoryFilter.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.ToLowerInvariant())
                .ToHashSet();
            if (allowed.Count > 0)
                items = items.Where(i => i.Category != null && allowed.Contains(i.Category.ToLowerInvariant())).ToList();
        }

        // Comerciales vigentes ahora mismo (activos, dentro de su ventana de fechas si tiene) —
        // el Menu Board no necesita saber de vigencia, solo recibe lo que ya aplica hoy. Pool
        // compartido por afiliado — todas las pantallas del mismo negocio ven los mismos
        // comerciales, solo cambia cada cuánto aparecen (AdFrequency, sí es por pantalla).
        var now = DateTime.UtcNow;
        var screenAds = await _db.ScreenAds
            .Where(a => a.AffiliateId == affiliate.Id && a.Active
                && (a.StartsAt == null || a.StartsAt <= now)
                && (a.EndsAt == null || a.EndsAt >= now))
            .OrderBy(a => a.SortOrder)
            .Select(a => new ScreenAdDto(a.Id, a.MediaUrl, a.MediaType.ToString(), a.DurationSeconds, a.SortOrder, a.Active, a.StartsAt, a.EndsAt))
            .ToListAsync();

        return new PublicCatalogResponse(
            await MapToAffiliatePublicDtoAsync(affiliate),
            items,
            BuildCapabilities(affiliate.Plan),
            screenAds,
            screen?.AdFrequency ?? affiliate.AdFrequency,
            screen?.Language ?? affiliate.Language,
            (screen?.BoardTheme ?? affiliate.BoardTheme).ToString());
    }

    public async Task<List<FeaturedAffiliateDto>> GetFeaturedAffiliatesAsync()
        => await _db.Affiliates
            .Where(a => a.IsFeatured && a.Published)
            .OrderBy(a => a.Name)
            .Select(a => new FeaturedAffiliateDto(a.Slug!, a.Name, a.Description, a.LogoUrl))
            .ToListAsync();

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
            a.Description, a.DescriptionEn, a.PrimaryColor, a.LogoUrl, a.CoverImageUrl,
            a.WhatsApp, a.ContactEmail, a.Address,
            null,   // City — Affiliate entity does not have this field yet
            a.Website,
            canales,
            JsonArrayField.Parse<ProcessStepDto>(a.ProcessSteps),
            JsonArrayField.Parse<FaqItemDto>(a.Faq),
            JsonArrayField.Parse<HorarioEntryDto>(a.Horario),
            a.Timezone);
    }

    // Single source of truth for what each plan unlocks — flip a value here to change it
    // everywhere it's checked (dashboard teasers, the public /board route gate, etc.),
    // instead of hardcoding a plan check at each call site.
    private static PlanCapabilitiesDto BuildCapabilities(Plan plan) =>
        plan == Plan.Entrepreneur
            ? new PlanCapabilitiesDto(true, true, true, true, true, true, true, MenuBoard: true)
            : new PlanCapabilitiesDto(false, false, false, false, false, false, false, MenuBoard: false);
}
