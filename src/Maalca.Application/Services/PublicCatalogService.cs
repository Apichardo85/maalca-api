using System.Text.Json;
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
    private readonly IModifierService _modifiers;

    public PublicCatalogService(AppDbContext db, IModifierService modifiers)
    {
        _db = db;
        _modifiers = modifiers;
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

        // Muro sincronizado: esta TV recibe el modo del muro y SU posición. Los comerciales (y su
        // frecuencia) salen SIEMPRE de la pantalla líder (posición 0,0): cruzan el muro todas juntas.
        // El menú depende de cómo lo configuró el dueño:
        //  - Espejo, o Muro sin categorías distintas por TV → todas usan el menú de la líder (el
        //    board reparte las slides entre TVs).
        //  - Muro con categorías/modo distintos por TV (PerTvContent) → cada TV muestra SU menú; el
        //    board alinea los cambios de slide y los comerciales con un timeline común por slots.
        PublicWallDto? wallDto = null;
        var adsScreen = screen;
        if (screen != null && screen.WallId is Guid wallId && screen.WallCol.HasValue && screen.WallRow.HasValue)
        {
            var wall = await _db.ScreenWalls.FirstOrDefaultAsync(w => w.Id == wallId);
            if (wall != null)
            {
                var members = await _db.Screens
                    .Where(s => s.WallId == wallId && s.WallCol != null && s.WallRow != null)
                    .OrderBy(s => s.WallRow).ThenBy(s => s.WallCol)
                    .ToListAsync();
                var lead = members.FirstOrDefault();
                var perTv = wall.Mode == ScreenWallMode.Span && lead != null
                    && members.Any(m => !SameMenuContent(m, lead));
                wallDto = new PublicWallDto(wall.Mode.ToString(), wall.Cols, wall.Rows,
                    screen.WallCol.Value, screen.WallRow.Value, wall.BezelPx, wall.Calibrate, perTv);
                if (lead != null)
                {
                    adsScreen = lead;
                    if (!perTv) screen = lead;
                }
            }
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

            // Grupos de modificadores reutilizables (ej. "Guarnición") — mismo patrón en batch que
            // la receta pública de arriba, un solo query para todos los platos de esta carga.
            var modifiersByProduct = await _modifiers.GetModifierGroupsForProductsAsync(affiliate.Id, productIds);
            if (modifiersByProduct.Count > 0)
            {
                items = items.Select(i => modifiersByProduct.TryGetValue(i.Id, out var groups)
                    ? i with { ModifierGroups = groups }
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

                BusinessType.Retail => await RetailItemsAsync(affiliate.Id),

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
        var adIdsFilter = adsScreen?.AdIds is null ? null : JsonArrayField.Parse<Guid>(adsScreen.AdIds).ToHashSet();
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
            BuildCapabilities(affiliate.Plan) with { StripeReady = affiliate.StripeConnectChargesEnabled && !string.IsNullOrEmpty(affiliate.StripeConnectAccountId), PickupNewCustomerLimit = affiliate.PickupNewCustomerLimit },
            screenAds,
            adsScreen?.AdFrequency ?? affiliate.AdFrequency,
            screen?.Language ?? affiliate.Language,
            (screen?.BoardTheme ?? affiliate.BoardTheme).ToString(),
            (screen?.TransitionEffect ?? affiliate.TransitionEffect).ToString(),
            (screen?.Layout ?? affiliate.BoardLayout).ToString(),
            wallDto);
    }

    /// <summary>¿Dos TVs de un muro muestran el mismo menú? (categorías sin importar orden ni
    /// mayúsculas + modo de contenido). Lo visual (tema, idioma) no cuenta: no cambia las slides.</summary>
    private static bool SameMenuContent(Domain.Entities.Screen a, Domain.Entities.Screen b)
    {
        static string Norm(string? f) => string.Join("|", (f ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.ToLowerInvariant()).OrderBy(c => c));
        return a.ContentMode == b.ContentMode && Norm(a.CategoryFilter) == Norm(b.CategoryFilter);
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
            a.ZoomLink,
            canales,
            JsonArrayField.Parse<ProcessStepDto>(a.ProcessSteps),
            JsonArrayField.Parse<FaqItemDto>(a.Faq),
            JsonArrayField.Parse<HorarioEntryDto>(a.Horario),
            a.Timezone,
            a.Currency,
            JsonDictField.Parse(a.SectionVisibility),
            JsonArrayField.Parse<string>(a.GalleryImages),
            JsonObjectField.Parse<CommunityImpactDto>(a.CommunityImpact),
            a.SecondaryColor,
            a.AccentColor,
            JsonObjectField.Parse<Dictionary<string, MealPeriodRangeDto>>(a.MealPeriodHours),
            JsonObjectField.Parse<Dictionary<string, CategoryTranslationDto>>(a.CategoryTranslations),
            ParseDefaultTheme(a.Settings),
            ParseSpanishFlag(a.Settings),
            a.Language,
            Maalca.Application.Common.ModuleCatalog
                .FilterActive(a.ModulosActivos, a.BusinessType.ToString())
                .Contains("orders", StringComparer.OrdinalIgnoreCase));
    }

    // Affiliate.Settings es un JSON libre ("{}" por defecto). Solo se lee "defaultTheme": "dark" | "light".
    // Cualquier otra cosa (JSON roto, otro tipo, otro valor) = null, o sea, sin tema forzado.
    private static string? ParseDefaultTheme(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("defaultTheme", out var v)
                && v.ValueKind == JsonValueKind.String)
            {
                var t = v.GetString()?.ToLowerInvariant();
                return t is "dark" or "light" ? t : null;
            }
        }
        catch (JsonException) { }
        return null;
    }

    // "spanishFlag": codigo ISO-3166 alpha-2 (ej. "MX") de la bandera que el toggle de idioma muestra
    // junto a "ES" en la pagina publica. Null = default de la plataforma (RD). Solo 2 letras A-Z.
    public static string? ParseSpanishFlag(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("spanishFlag", out var v)
                && v.ValueKind == JsonValueKind.String)
            {
                var c = v.GetString()?.Trim().ToUpperInvariant();
                return c is { Length: 2 } && c.All(ch => ch is >= 'A' and <= 'Z') ? c : null;
            }
        }
        catch (JsonException) { }
        return null;
    }

    // Single source of truth for what each plan unlocks — flip a value here to change it
    // everywhere it's checked (dashboard teasers, the public /board route gate, etc.),
    // instead of hardcoding a plan check at each call site.
    private static PlanCapabilitiesDto BuildCapabilities(Plan plan) =>
        // Enterprise es un tier por ENCIMA de Emprendedor: desbloquea todo lo que desbloquea Emprendedor.
        plan is Plan.Entrepreneur or Plan.Enterprise
            ? new PlanCapabilitiesDto(true, true, true, true, true, true, true, MenuBoard: true)
            : new PlanCapabilitiesDto(false, false, false, false, false, false, false, MenuBoard: false);

    /// <summary>
    /// Catálogo Retail (InventoryItem). En items con control de stock, Stock ya descuenta las unidades
    /// reservadas por pedidos "pagar al recoger" abiertos y checkouts de Stripe vigentes (OrderService.ReservedUnitsAsync), para que
    /// "Quedan N" / "Agotado" en la tienda coincidan con lo que el API aceptará al pedir.
    /// </summary>
    private async Task<List<CatalogItemDto>> RetailItemsAsync(Guid affiliateId)
    {
        var inventory = await _db.InventoryItems
            .Where(i => i.AffiliateId == affiliateId && i.IsPubliclyVisible && i.Status == "Active")
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Name)
            .ToListAsync();
        var dtos = inventory.Select(CatalogItemMapper.FromInventoryItem).ToList();
        if (!inventory.Any(i => i.TrackStock)) return dtos;

        var reserved = await OrderService.ReservedUnitsAsync(_db, affiliateId);

        return dtos
            .Select(d => d.TrackStock == true && d.Stock.HasValue
                ? d with { Stock = Math.Max(0, d.Stock.Value - (int)Math.Ceiling(reserved.GetValueOrDefault(d.Id))) }
                : d)
            .ToList();
    }
}
