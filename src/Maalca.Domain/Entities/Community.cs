using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

// ── MaalCa Comunidad (businessType = Community), Fase 1 ─────────────────────
// Recipe/Combo reutilizan el InventoryItem existente (misma tabla que Retail/Restaurante), no
// un inventario paralelo. Distintos de Product/ProductIngredient a propósito: aquí no hay venta
// ni precio, sino porciones y costo real por plato servido.

/// <summary>Receta que rinde <see cref="Servings"/> porciones. Las cantidades de sus
/// ingredientes son para la receta COMPLETA (el lote), no por porción.</summary>
public class Recipe : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Servings { get; set; } = 1;
    /// <summary>Calculado server-side: sum(Ingredient.QuantityRequired * InventoryItem.UnitCost) / Servings.
    /// Nunca se acepta del cliente — ver CommunityService.RecalculateRecipeCostAsync.</summary>
    public decimal CostPerServing { get; set; }

    public Affiliate? Affiliate { get; set; }
    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
}

public class RecipeIngredient : BaseEntity
{
    public Guid RecipeId { get; set; }
    public Guid InventoryItemId { get; set; }
    /// <summary>Cantidad para la receta completa (rinde Recipe.Servings porciones).</summary>
    public decimal QuantityRequired { get; set; }

    public Recipe? Recipe { get; set; }
    public InventoryItem? InventoryItem { get; set; }
}

/// <summary>Plato servido = una porción de cada Recipe del combo (ej. arroz + habichuelas + pollo).</summary>
public class Combo : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Calculado server-side: sum(Recipe.CostPerServing) de las recetas del combo.</summary>
    public decimal CostPerPlate { get; set; }

    public Affiliate? Affiliate { get; set; }
    public ICollection<ComboRecipe> Recipes { get; set; } = new List<ComboRecipe>();
}

/// <summary>Join Combo ↔ Recipe (el "recipeIds: guid[]" del spec) — tabla con FKs reales en vez
/// de un uuid[] para que borrar una Recipe no deje ids colgando dentro de un array.</summary>
public class ComboRecipe
{
    public Guid ComboId { get; set; }
    public Guid RecipeId { get; set; }

    public Combo? Combo { get; set; }
    public Recipe? Recipe { get; set; }
}

/// <summary>Una llamada a POST /combos/{id}/serve. Es la ÚNICA fuente de "comidas servidas" —
/// community-metrics suma Quantity de estas filas; no existe un contador aparte que pueda
/// desincronizarse. CostPerPlate es snapshot del momento de servir.</summary>
public class ComboServing : BaseEntity
{
    public Guid AffiliateId { get; set; }
    // Nullable + SetNull: borrar un Combo no debe borrar el historial de comidas servidas.
    public Guid? ComboId { get; set; }
    public string ComboName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal CostPerPlate { get; set; }
    public DateTime ServedAt { get; set; } = DateTime.UtcNow;

    public Affiliate? Affiliate { get; set; }
    public Combo? Combo { get; set; }
}
