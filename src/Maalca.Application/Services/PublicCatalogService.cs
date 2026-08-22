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
        // IsActive=false = suspendido desde /ops (Fase 60) — se comporta igual que "no publicado"
        // de cara al público, sin tocar la preferencia real del dueño en Published.
        var affiliate = await _db.Affiliates
            .Where(a => a.Slug == slug && a.Published && a.IsActive)
            .FirstOrDefaultAsync();

        if (affiliate == null) return null;

        return await MapToAffiliatePublicDtoAsync(affiliate);
    }

    public async Task<PublicCatalogResponse?> GetCatalogAsync(string slug, Guid? screenId = null)
    {
        var affiliate = await _db.Affiliates
            .Where(a => a.Slug == slug && a.Published && a.IsActive)
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
        // Status == "Active" además de IsPubliclyVisible — un item "Inactivo" (ej. un plato que
        // hoy no se puede servir) desaparece tanto de la página pública como del Menu Board a la
        // vez, desde el mismo punto: antes solo MenuBoard.tsx filtraba esto en el cliente, la
        // página pública no lo hacía en ningún lado, así que un item inactivo se seguía viendo ahí.
        if (affiliate.BusinessType is BusinessType.Restaurant or BusinessType.Creator or BusinessType.Publisher)
        {
            var products = await _db.Products
                .Where(p => p.AffiliateId == affiliate.Id && p.IsPubliclyVisible && p.Status == "Active")
                .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
                .ToListAsync();
            items = products.Select(CatalogItemMapper.FromProduct).ToList();

            // Receta pública (solo lectura, solo nombres) — para que el kiosko/página pública
            // pueda mostrar "contiene: X, Y" y dejar al cliente destildar lo que no quiera.
            // Un solo query en batch para todos los platos de esta carga, no N+1 por item.
            var productIds = products.Select(p => p.Id).ToList();
            if (productIds.Count > 0)
            {
                var ingredientsByProduct = (await _db.ProductIngredients
                    .Where(pi => productIds.Contains(pi.ProductId) && pi.InventoryItem != null)
                    .Include(pi => pi.InventoryItem)
                    .Select(pi => new { pi.ProductId, pi.InventoryItemId, Name = pi.InventoryItem!.Name })
                    .ToListAsync())
                    .GroupBy(x => x.ProductId)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<PublicIngredientDto>)g
                        .Select(x => new PublicIngredientDto(x.InventoryItemId, x.Name))
                        .ToList());

                items = items.Select(i => ingredientsByProduct.TryGetValue(i.Id, out var ings)
                    ? i with { Ingredients = ings }
                    : i).ToList();
            }
        }
        else
        {
            items = affiliate.BusinessType switch
            {
                BusinessType.Barber or BusinessType.Service or BusinessType.Professional =>
                    (await _db.Services
                        .Where(s => s.AffiliateId == affiliate.Id && s.IsPubliclyVisible && s.Status == "Active")
                        .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
                        .ToListAsync())
                        .Select(CatalogItemMapper.FromService).ToList(),

                BusinessType.Retail =>
                    (await _db.InventoryItems
                        .Where(i => i.AffiliateId == affiliate.Id && i.IsPubliclyVisible && i.Status == "Active")
                        .OrderBy(i => i.SortOrder).ThenBy(i => i.Name)
                        .ToListAsync())
                        .Select(CatalogItemMapper.FromInventoryItem).ToList(),

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

        // Fase 9 Etapa C — ContentMode de la pantalla: AdsOnly vacía el menú por completo
        // (la pantalla rota solo comerciales); FeaturedOnly lo recorta a items destacados
        // (Featured solo existe en Product/Restaurant — en otros tipos de negocio queda vacío,
        // caso esperado, no un bug). Menu (default) no toca items.
        if (screen?.ContentMode == ScreenContentMode.AdsOnly)
        {
            items = new List<CatalogItemDto>();
        }
        else if (screen?.ContentMode == ScreenContentMode.FeaturedOnly)
        {
            items = items.Where(i => i.Featured == true).ToList();
        }

        // Comerciales vigentes ahora mismo (activos, dentro de su ventana de fechas si tiene) —
        // el Menu Board no necesita saber de vigencia, solo recibe lo que ya aplica hoy. Pool
        // por afiliado, pero cada pantalla puede filtrarlo con AdIds (Fase 9 Etapa C): null =
        // hereda todos (comportamiento previo); lista = solo esos IDs (puede ser vacía = ninguno).
        var now = DateTime.UtcNow;
        var adIdsFilter = screen?.AdIds is null ? null : JsonArrayField.Parse<Guid>(screen.AdIds).ToHashSet();
        var screenAds = (await _db.ScreenAds
            .Where(a => a.AffiliateId == affiliate.Id && a.Active
                && (a.StartsAt == null || a.StartsAt <= now)
                && (a.EndsAt == null || a.EndsAt >= now))
            .OrderBy(a => a.SortOrder)
            .ToListAsync())
            .Where(a => adIdsFilter == null || adIdsFilter.Contains(a.Id))
            .Select(a => new ScreenAdDto(a.Id, a.MediaUrl, a.MediaType.ToString(), a.DurationSeconds, a.SortOrder, a.Active, a.StartsAt, a.EndsAt, a.Fit.ToString()))
            .ToList();

        return new PublicCatalogResponse(
            await MapToAffiliatePublicDtoAsync(affiliate),
            items,
            BuildCapabilities(affiliate.Plan),
            screenAds,
            screen?.AdFrequency ?? affiliate.AdFrequency,
            screen?.Language ?? affiliate.Language,
            (screen?.BoardTheme ?? affiliate.BoardTheme).ToString(),
            (screen?.TransitionEffect ?? affiliate.TransitionEffect).ToString());
    }

    public async Task<List<FeaturedAffiliateDto>> GetFeaturedAffiliatesAsync()
        => await _db.Affiliates
            .Where(a => a.IsFeatured && a.Published && a.IsActive)
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
            a.Timezone,
            a.Currency,
            JsonDictField.Parse(a.SectionVisibility),
            JsonArrayField.Parse<string>(a.GalleryImages));
    }

    // Single source of truth for what each plan unlocks — flip a value here to change it
    // everywhere it's checked (dashboard teasers, the public /board route gate, etc.),
    // instead of hardcoding a plan check at each call site.
    private static PlanCapabilitiesDto BuildCapabilities(Plan plan) =>
        plan == Plan.Entrepreneur
            ? new PlanCapabilitiesDto(true, true, true, true, true, true, true, MenuBoard: true)
            : new PlanCapabilitiesDto(false, false, false, false, false, false, false, MenuBoard: false);
}
