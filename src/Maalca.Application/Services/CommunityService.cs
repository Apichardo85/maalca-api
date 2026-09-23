using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

/// <summary>MaalCa Comunidad (Fase 1). Reutiliza la tabla InventoryItems existente (no un
/// inventario paralelo) — un item creado aquí también aparece en /inventory y viceversa.
///
/// Convención de cantidades: RecipeIngredient.QuantityRequired es para la receta COMPLETA, que
/// rinde Recipe.Servings porciones. Por eso CostPerServing = sum(qty * unitCost) / Servings, y
/// servir N platos descuenta qty / Servings * N de cada ingrediente — así el costo por plato y
/// el inventario descontado salen de la misma base.</summary>
public class CommunityService : ICommunityService
{
    private const int CostDecimals = 4;
    private const int QuantityDecimals = 3;
    private const int MaxServePerCall = 10_000;

    private readonly AppDbContext _db;

    public CommunityService(AppDbContext db) => _db = db;

    // ── Inventory items ─────────────────────────────────────────────────────

    public async Task<List<CommunityInventoryItemDto>> GetInventoryItemsAsync(Guid affiliateId, DateOnly? expiringBefore = null)
    {
        var query = _db.InventoryItems.Where(i => i.AffiliateId == affiliateId);
        if (expiringBefore is DateOnly cutoff)
        {
            // Inclusivo: ?expiringBefore=2026-10-01 incluye lo que vence ese mismo día — para una
            // alerta de vencimiento es preferible avisar de más que de menos.
            return (await query
                    .Where(i => i.ExpirationDate != null && i.ExpirationDate <= cutoff)
                    .OrderBy(i => i.ExpirationDate).ThenBy(i => i.Name)
                    .ToListAsync())
                .Select(ToDto).ToList();
        }

        return (await query.OrderBy(i => i.Name).ToListAsync()).Select(ToDto).ToList();
    }

    public async Task<CommunityInventoryItemDto?> GetInventoryItemAsync(Guid affiliateId, Guid id)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.AffiliateId == affiliateId);
        return item is null ? null : ToDto(item);
    }

    public async Task<CommunityInventoryItemDto> CreateInventoryItemAsync(Guid affiliateId, UpsertCommunityInventoryItemRequest request)
    {
        ValidateInventoryRequest(request);
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            AffiliateId = affiliateId,
            CreatedAt = DateTime.UtcNow,
            Status = "Active",
        };
        // Mismo formato que InventoryService.CreateInventoryItemAsync — todo item tiene código interno.
        item.InternalCode = $"INT-{item.Id.ToString()[..8].ToUpperInvariant()}";
        ApplyInventoryRequest(item, request);

        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync();
        return ToDto(item);
    }

    public async Task<CommunityInventoryItemDto?> UpdateInventoryItemAsync(Guid affiliateId, Guid id, UpsertCommunityInventoryItemRequest request)
    {
        ValidateInventoryRequest(request);
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.AffiliateId == affiliateId);
        if (item is null) return null;

        var unitCostChanged = item.UnitCost != request.UnitCost;
        ApplyInventoryRequest(item, request);
        item.UpdatedAt = DateTime.UtcNow;

        // El costo de las recetas/combos que usan este insumo depende de UnitCost — se recalcula
        // aquí para que CostPerServing/CostPerPlate nunca queden viejos.
        if (unitCostChanged)
        {
            var recipeIds = await _db.RecipeIngredients
                .Where(ri => ri.InventoryItemId == id)
                .Select(ri => ri.RecipeId)
                .Distinct()
                .ToListAsync();
            await RecalculateCostsAsync(affiliateId, recipeIds);
        }

        await _db.SaveChangesAsync();
        return ToDto(item);
    }

    public async Task<bool> DeleteInventoryItemAsync(Guid affiliateId, Guid id)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.AffiliateId == affiliateId);
        if (item is null) return false;

        var usedIn = await _db.RecipeIngredients
            .Where(ri => ri.InventoryItemId == id)
            .Select(ri => ri.Recipe!.Name)
            .Union(_db.ProductIngredients
                .Where(pi => pi.InventoryItemId == id && pi.Product != null)
                .Select(pi => pi.Product!.Name))
            .ToListAsync();
        if (usedIn.Count > 0)
            throw new InvalidOperationException(
                $"No se puede eliminar \"{item.Name}\": está en la receta de {string.Join(", ", usedIn)}. Quítalo de esa receta primero.");

        _db.InventoryItems.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Recipes ─────────────────────────────────────────────────────────────

    public async Task<List<RecipeDto>> GetRecipesAsync(Guid affiliateId)
        => (await RecipesWithIngredients()
                .Where(r => r.AffiliateId == affiliateId)
                .OrderBy(r => r.Name)
                .ToListAsync())
            .Select(ToDto).ToList();

    public async Task<RecipeDto?> GetRecipeAsync(Guid affiliateId, Guid id)
    {
        var recipe = await LoadRecipeAsync(affiliateId, id);
        return recipe is null ? null : ToDto(recipe);
    }

    public async Task<RecipeDto> CreateRecipeAsync(Guid affiliateId, UpsertRecipeRequest request)
    {
        ValidateRecipeRequest(request);
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            AffiliateId = affiliateId,
            Name = request.Name.Trim(),
            Servings = request.Servings,
            CostPerServing = 0, // sin ingredientes todavía
            CreatedAt = DateTime.UtcNow,
        };
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();
        return ToDto(recipe);
    }

    public async Task<RecipeDto?> UpdateRecipeAsync(Guid affiliateId, Guid id, UpsertRecipeRequest request)
    {
        ValidateRecipeRequest(request);
        var recipe = await LoadRecipeAsync(affiliateId, id);
        if (recipe is null) return null;

        recipe.Name = request.Name.Trim();
        recipe.Servings = request.Servings;
        recipe.UpdatedAt = DateTime.UtcNow;
        await RecalculateCostsAsync(affiliateId, new[] { id });
        await _db.SaveChangesAsync();
        return ToDto(recipe);
    }

    public async Task<bool> DeleteRecipeAsync(Guid affiliateId, Guid id)
    {
        var recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.Id == id && r.AffiliateId == affiliateId);
        if (recipe is null) return false;

        var usedInCombos = await _db.ComboRecipes
            .Where(cr => cr.RecipeId == id)
            .Select(cr => cr.Combo!.Name)
            .ToListAsync();
        if (usedInCombos.Count > 0)
            throw new InvalidOperationException(
                $"No se puede eliminar \"{recipe.Name}\": está en el combo {string.Join(", ", usedInCombos)}. Quítala de ese combo primero.");

        _db.Recipes.Remove(recipe); // RecipeIngredients en cascada
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Recipe ingredients ──────────────────────────────────────────────────

    public async Task<List<RecipeIngredientDto>?> GetRecipeIngredientsAsync(Guid affiliateId, Guid recipeId)
    {
        var recipe = await LoadRecipeAsync(affiliateId, recipeId);
        return recipe is null ? null : ToDto(recipe).Ingredients.ToList();
    }

    public async Task<RecipeDto?> AddRecipeIngredientAsync(Guid affiliateId, Guid recipeId, UpsertRecipeIngredientRequest request)
    {
        var recipe = await LoadRecipeAsync(affiliateId, recipeId);
        if (recipe is null) return null;
        await ValidateIngredientRequestAsync(affiliateId, request);

        if (recipe.Ingredients.Any(ri => ri.InventoryItemId == request.InventoryItemId))
            throw new InvalidOperationException("Ese insumo ya está en la receta — edita su cantidad en vez de agregarlo otra vez.");

        _db.RecipeIngredients.Add(new RecipeIngredient
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeId,
            InventoryItemId = request.InventoryItemId,
            QuantityRequired = request.QuantityRequired,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        return await SaveRecalculatedRecipeAsync(affiliateId, recipeId);
    }

    public async Task<RecipeDto?> UpdateRecipeIngredientAsync(Guid affiliateId, Guid recipeId, Guid ingredientId, UpsertRecipeIngredientRequest request)
    {
        var recipe = await LoadRecipeAsync(affiliateId, recipeId);
        var line = recipe?.Ingredients.FirstOrDefault(ri => ri.Id == ingredientId);
        if (line is null) return null;
        await ValidateIngredientRequestAsync(affiliateId, request);

        if (request.InventoryItemId != line.InventoryItemId &&
            recipe!.Ingredients.Any(ri => ri.InventoryItemId == request.InventoryItemId))
            throw new InvalidOperationException("Ese insumo ya está en la receta.");

        line.InventoryItemId = request.InventoryItemId;
        line.QuantityRequired = request.QuantityRequired;
        line.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await SaveRecalculatedRecipeAsync(affiliateId, recipeId);
    }

    public async Task<RecipeDto?> DeleteRecipeIngredientAsync(Guid affiliateId, Guid recipeId, Guid ingredientId)
    {
        var recipe = await LoadRecipeAsync(affiliateId, recipeId);
        var line = recipe?.Ingredients.FirstOrDefault(ri => ri.Id == ingredientId);
        if (line is null) return null;

        _db.RecipeIngredients.Remove(line);
        await _db.SaveChangesAsync();
        return await SaveRecalculatedRecipeAsync(affiliateId, recipeId);
    }

    // ── Combos ──────────────────────────────────────────────────────────────

    public async Task<List<ComboDto>> GetCombosAsync(Guid affiliateId)
        => (await _db.Combos
                .Include(c => c.Recipes)
                .Where(c => c.AffiliateId == affiliateId)
                .OrderBy(c => c.Name)
                .ToListAsync())
            .Select(ToDto).ToList();

    public async Task<ComboDto?> GetComboAsync(Guid affiliateId, Guid id)
    {
        var combo = await _db.Combos.Include(c => c.Recipes)
            .FirstOrDefaultAsync(c => c.Id == id && c.AffiliateId == affiliateId);
        return combo is null ? null : ToDto(combo);
    }

    public async Task<ComboDto> CreateComboAsync(Guid affiliateId, UpsertComboRequest request)
    {
        var recipeIds = await ValidateComboRequestAsync(affiliateId, request);
        var combo = new Combo
        {
            Id = Guid.NewGuid(),
            AffiliateId = affiliateId,
            Name = request.Name.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        foreach (var rid in recipeIds)
            combo.Recipes.Add(new ComboRecipe { ComboId = combo.Id, RecipeId = rid });
        _db.Combos.Add(combo);
        await _db.SaveChangesAsync();

        await RecalculateComboCostsAsync(affiliateId, new[] { combo.Id });
        await _db.SaveChangesAsync();
        return ToDto(combo);
    }

    public async Task<ComboDto?> UpdateComboAsync(Guid affiliateId, Guid id, UpsertComboRequest request)
    {
        var combo = await _db.Combos.Include(c => c.Recipes)
            .FirstOrDefaultAsync(c => c.Id == id && c.AffiliateId == affiliateId);
        if (combo is null) return null;
        var recipeIds = await ValidateComboRequestAsync(affiliateId, request);

        combo.Name = request.Name.Trim();
        combo.UpdatedAt = DateTime.UtcNow;
        // Reemplazo total de la lista, igual que PUT /products/{id}/ingredients.
        foreach (var cr in combo.Recipes.Where(cr => !recipeIds.Contains(cr.RecipeId)).ToList())
            combo.Recipes.Remove(cr);
        foreach (var rid in recipeIds.Where(rid => combo.Recipes.All(cr => cr.RecipeId != rid)))
            combo.Recipes.Add(new ComboRecipe { ComboId = combo.Id, RecipeId = rid });
        await _db.SaveChangesAsync();

        await RecalculateComboCostsAsync(affiliateId, new[] { combo.Id });
        await _db.SaveChangesAsync();
        return ToDto(combo);
    }

    public async Task<bool> DeleteComboAsync(Guid affiliateId, Guid id)
    {
        var combo = await _db.Combos.FirstOrDefaultAsync(c => c.Id == id && c.AffiliateId == affiliateId);
        if (combo is null) return false;
        // ComboRecipes en cascada; ComboServings quedan (ComboId -> null, ComboName conservado)
        // para que las comidas ya servidas sigan contando en community-metrics.
        _db.Combos.Remove(combo);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Serve ───────────────────────────────────────────────────────────────

    public async Task<ServeComboResponse?> ServeComboAsync(Guid affiliateId, Guid comboId, int quantity)
    {
        if (quantity < 1 || quantity > MaxServePerCall)
            throw new InvalidOperationException($"quantity debe estar entre 1 y {MaxServePerCall}.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        var combo = await _db.Combos
            .Include(c => c.Recipes).ThenInclude(cr => cr.Recipe!).ThenInclude(r => r.Ingredients)
            .FirstOrDefaultAsync(c => c.Id == comboId && c.AffiliateId == affiliateId);
        if (combo is null) return null;

        var recipes = combo.Recipes.Select(cr => cr.Recipe!).ToList();
        if (recipes.Count == 0)
            throw new InvalidOperationException("El combo no tiene recetas.");

        // Consumo total por insumo — un mismo insumo puede aparecer en varias recetas del combo
        // (ej. aceite en el arroz y en el pollo), así que se agrega antes de descontar.
        var consumption = recipes
            .SelectMany(r => r.Ingredients.Select(ri => (ri.InventoryItemId, Amount: ri.QuantityRequired / r.Servings * quantity)))
            .GroupBy(x => x.InventoryItemId)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(x => x.Amount), QuantityDecimals, MidpointRounding.AwayFromZero));

        // Bloqueo de fila (FOR UPDATE) sobre los insumos a descontar: dos /serve simultáneos del
        // mismo afiliado no deben pisarse el read-modify-write de Quantity.
        var itemIds = consumption.Keys.ToArray();
        var items = await _db.InventoryItems
            .FromSqlInterpolated($@"SELECT * FROM ""InventoryItems"" WHERE ""Id"" = ANY({itemIds}) AND ""AffiliateId"" = {affiliateId} FOR UPDATE")
            .ToListAsync();

        // Costo fresco al momento de servir (UnitCost actual), no el último valor guardado.
        foreach (var r in recipes) r.CostPerServing = ComputeRecipeCost(r, items);
        combo.CostPerPlate = ComputeComboCost(recipes);

        var now = DateTime.UtcNow;
        var serving = new ComboServing
        {
            Id = Guid.NewGuid(),
            AffiliateId = affiliateId,
            ComboId = combo.Id,
            ComboName = combo.Name,
            Quantity = quantity,
            CostPerPlate = combo.CostPerPlate,
            ServedAt = now,
            CreatedAt = now,
        };
        _db.ComboServings.Add(serving);

        var result = new List<InventoryConsumptionDto>();
        foreach (var item in items.OrderBy(i => i.Name))
        {
            var requested = consumption[item.Id];
            if (requested <= 0) continue;
            var deducted = Math.Min(item.Quantity, requested);
            var shortage = requested > item.Quantity;
            item.Quantity -= deducted;
            item.UpdatedAt = now;

            _db.InventoryMovements.Add(new InventoryMovement
            {
                Id = Guid.NewGuid(),
                InventoryItemId = item.Id,
                Type = "out",
                Quantity = deducted,
                Notes = shortage
                    ? $"Servido — {combo.Name} x{quantity} (faltaron {requested - deducted} {item.Unit})"
                    : $"Servido — {combo.Name} x{quantity}",
                CreatedAt = now,
            });
            result.Add(new InventoryConsumptionDto(item.Id, item.Name, item.Unit, requested, item.Quantity, shortage));
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return new ServeComboResponse(
            serving.Id, combo.Id, quantity, combo.CostPerPlate,
            Math.Round(combo.CostPerPlate * quantity, 2, MidpointRounding.AwayFromZero),
            now, result);
    }

    // ── Metrics ─────────────────────────────────────────────────────────────

    public async Task<CommunityMetricsDto> GetMetricsAsync(Guid affiliateId)
    {
        var timezone = await _db.Affiliates
            .Where(a => a.Id == affiliateId)
            .Select(a => a.Timezone)
            .FirstOrDefaultAsync();
        var (startUtc, endUtc) = CurrentMonthUtc(timezone);

        // Derivado exclusivamente de las llamadas a /serve — no hay contador aparte.
        var servings = _db.ComboServings
            .Where(s => s.AffiliateId == affiliateId && s.ServedAt >= startUtc && s.ServedAt < endUtc);
        var meals = await servings.SumAsync(s => (int?)s.Quantity) ?? 0;
        var cost = await servings.SumAsync(s => (decimal?)(s.Quantity * s.CostPerPlate)) ?? 0m;

        return new CommunityMetricsDto(meals, Math.Round(cost, 2, MidpointRounding.AwayFromZero), startUtc, endUtc);
    }

    public async Task<PublicCommunityMetricsDto?> GetPublicMetricsAsync(string slug)
    {
        var affiliate = await _db.Affiliates
            .Where(a => a.Slug == slug && a.BusinessType == BusinessType.Community)
            .Select(a => new { a.Id, a.Timezone })
            .FirstOrDefaultAsync();
        if (affiliate is null) return null;

        var (startUtc, endUtc) = CurrentMonthUtc(affiliate.Timezone);
        var meals = await _db.ComboServings
            .Where(s => s.AffiliateId == affiliate.Id && s.ServedAt >= startUtc && s.ServedAt < endUtc)
            .SumAsync(s => (int?)s.Quantity) ?? 0;

        // Promedio simple entre los combos que ya tienen costo calculado (CostPerPlate > 0) —
        // un combo recién creado sin recetas cargadas todavía no cuenta, para no arrastrar el
        // promedio a la baja con un $0 que no es real.
        var costs = await _db.Combos
            .Where(c => c.AffiliateId == affiliate.Id && c.CostPerPlate > 0)
            .Select(c => c.CostPerPlate)
            .ToListAsync();
        decimal? avgCost = costs.Count > 0
            ? Math.Round(costs.Average(), 2, MidpointRounding.AwayFromZero)
            : null;

        return new PublicCommunityMetricsDto(meals, avgCost);
    }

    // ── Cálculo de costos ───────────────────────────────────────────────────

    private static decimal ComputeRecipeCost(Recipe recipe, IEnumerable<InventoryItem> items)
    {
        if (recipe.Servings <= 0) return 0;
        var byId = items.ToDictionary(i => i.Id);
        var total = recipe.Ingredients.Sum(ri =>
            ri.QuantityRequired * (byId.TryGetValue(ri.InventoryItemId, out var inv) ? inv.UnitCost : ri.InventoryItem?.UnitCost ?? 0));
        return Math.Round(total / recipe.Servings, CostDecimals, MidpointRounding.AwayFromZero);
    }

    private static decimal ComputeComboCost(IEnumerable<Recipe> recipes)
        => Math.Round(recipes.Sum(r => r.CostPerServing), CostDecimals, MidpointRounding.AwayFromZero);

    /// <summary>Recalcula CostPerServing de las recetas dadas y CostPerPlate de todo combo que las
    /// use. No guarda — el llamador hace SaveChanges.</summary>
    private async Task RecalculateCostsAsync(Guid affiliateId, IEnumerable<Guid> recipeIds)
    {
        var ids = recipeIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var recipes = await RecipesWithIngredients()
            .Where(r => r.AffiliateId == affiliateId && ids.Contains(r.Id))
            .ToListAsync();
        foreach (var r in recipes)
            r.CostPerServing = ComputeRecipeCost(r, r.Ingredients.Where(ri => ri.InventoryItem != null).Select(ri => ri.InventoryItem!));

        var comboIds = await _db.ComboRecipes
            .Where(cr => ids.Contains(cr.RecipeId))
            .Select(cr => cr.ComboId)
            .Distinct()
            .ToListAsync();
        await RecalculateComboCostsAsync(affiliateId, comboIds);
    }

    private async Task RecalculateComboCostsAsync(Guid affiliateId, IEnumerable<Guid> comboIds)
    {
        var ids = comboIds.Distinct().ToList();
        if (ids.Count == 0) return;

        // Recetas ya cargadas/recalculadas en este DbContext se reutilizan (identity map), así el
        // combo suma el CostPerServing recién calculado y no el guardado.
        var combos = await _db.Combos
            .Include(c => c.Recipes).ThenInclude(cr => cr.Recipe)
            .Where(c => c.AffiliateId == affiliateId && ids.Contains(c.Id))
            .ToListAsync();
        foreach (var c in combos)
        {
            c.CostPerPlate = ComputeComboCost(c.Recipes.Where(cr => cr.Recipe != null).Select(cr => cr.Recipe!));
            c.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task<RecipeDto?> SaveRecalculatedRecipeAsync(Guid affiliateId, Guid recipeId)
    {
        await RecalculateCostsAsync(affiliateId, new[] { recipeId });
        await _db.SaveChangesAsync();
        return await GetRecipeAsync(affiliateId, recipeId);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private IQueryable<Recipe> RecipesWithIngredients()
        => _db.Recipes.Include(r => r.Ingredients).ThenInclude(ri => ri.InventoryItem);

    private Task<Recipe?> LoadRecipeAsync(Guid affiliateId, Guid id)
        => RecipesWithIngredients().FirstOrDefaultAsync(r => r.Id == id && r.AffiliateId == affiliateId);

    private static (DateTime StartUtc, DateTime EndUtc) CurrentMonthUtc(string? ianaTimezone)
    {
        var tz = TimeZoneInfo.Utc;
        if (!string.IsNullOrWhiteSpace(ianaTimezone))
        {
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(ianaTimezone); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var localStart = new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var start = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
        var end = TimeZoneInfo.ConvertTimeToUtc(localStart.AddMonths(1), tz);
        return (start, end);
    }

    private static void ValidateInventoryRequest(UpsertCommunityInventoryItemRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 200)
            throw new InvalidOperationException("El nombre es obligatorio (máx. 200 caracteres).");
        if (r.Unit is { } unit && unit.Trim().Length > 20)
            throw new InvalidOperationException("La unidad no puede pasar de 20 caracteres.");
        if (r.QuantityOnHand < 0 || r.UnitCost < 0 || r.LowStockThreshold < 0)
            throw new InvalidOperationException("Cantidad, costo y umbral no pueden ser negativos.");
        if (r.Source is not null && ParseSource(r.Source) is null)
            throw new InvalidOperationException("source debe ser donation_kind, purchased_cash o garden.");
    }

    private static void ApplyInventoryRequest(InventoryItem item, UpsertCommunityInventoryItemRequest r)
    {
        item.Name = r.Name.Trim();
        item.Unit = string.IsNullOrWhiteSpace(r.Unit) ? "unidad" : r.Unit.Trim().ToLowerInvariant();
        item.Quantity = r.QuantityOnHand;
        item.UnitCost = r.UnitCost;
        item.ExpirationDate = r.ExpirationDate;
        item.Source = r.Source is null ? null : ParseSource(r.Source);
        item.MinStock = r.LowStockThreshold ?? 0;
    }

    private static void ValidateRecipeRequest(UpsertRecipeRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 200)
            throw new InvalidOperationException("El nombre es obligatorio (máx. 200 caracteres).");
        if (r.Servings < 1)
            throw new InvalidOperationException("servings debe ser al menos 1.");
    }

    private async Task ValidateIngredientRequestAsync(Guid affiliateId, UpsertRecipeIngredientRequest r)
    {
        if (r.QuantityRequired <= 0)
            throw new InvalidOperationException("quantityRequired debe ser mayor que 0.");
        // El insumo debe ser de ESTE afiliado — evita enlazar un id de otro negocio a mano.
        var belongs = await _db.InventoryItems.AnyAsync(i => i.Id == r.InventoryItemId && i.AffiliateId == affiliateId);
        if (!belongs)
            throw new InvalidOperationException("Ese insumo no existe en el inventario de este afiliado.");
    }

    private async Task<List<Guid>> ValidateComboRequestAsync(Guid affiliateId, UpsertComboRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 200)
            throw new InvalidOperationException("El nombre es obligatorio (máx. 200 caracteres).");
        var ids = (r.RecipeIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (ids.Count == 0)
            throw new InvalidOperationException("Un combo necesita al menos una receta.");
        var valid = await _db.Recipes.CountAsync(x => x.AffiliateId == affiliateId && ids.Contains(x.Id));
        if (valid != ids.Count)
            throw new InvalidOperationException("Una o más recetas no existen para este afiliado.");
        return ids;
    }

    private static InventorySource? ParseSource(string value) => value.Trim().ToLowerInvariant() switch
    {
        "donation_kind" => InventorySource.DonationKind,
        "purchased_cash" => InventorySource.PurchasedCash,
        "garden" => InventorySource.Garden,
        _ => null,
    };

    private static string? FormatSource(InventorySource? source) => source switch
    {
        InventorySource.DonationKind => "donation_kind",
        InventorySource.PurchasedCash => "purchased_cash",
        InventorySource.Garden => "garden",
        _ => null,
    };

    private static CommunityInventoryItemDto ToDto(InventoryItem i)
    {
        decimal? threshold = i.MinStock > 0 ? i.MinStock : null;
        return new CommunityInventoryItemDto(
            i.Id, i.Name, i.Unit, i.Quantity, i.UnitCost, i.ExpirationDate, FormatSource(i.Source),
            threshold, threshold is not null && i.Quantity <= threshold, i.CreatedAt, i.UpdatedAt);
    }

    private static RecipeDto ToDto(Recipe r) => new(
        r.Id, r.Name, r.Servings, r.CostPerServing,
        r.Ingredients
            .Where(ri => ri.InventoryItem != null)
            .OrderBy(ri => ri.InventoryItem!.Name)
            .Select(ri => new RecipeIngredientDto(
                ri.Id, ri.InventoryItemId, ri.InventoryItem!.Name, ri.InventoryItem.Unit,
                ri.QuantityRequired, ri.InventoryItem.UnitCost,
                Math.Round(ri.QuantityRequired * ri.InventoryItem.UnitCost, CostDecimals, MidpointRounding.AwayFromZero)))
            .ToList());

    private static ComboDto ToDto(Combo c) => new(
        c.Id, c.Name, c.Recipes.Select(cr => cr.RecipeId).ToList(), c.CostPerPlate);
}
