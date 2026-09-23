namespace Maalca.Application.Common.DTOs;

// ── MaalCa Comunidad (Fase 1) ────────────────────────────────────────────────
// Vista "Comunidad" del InventoryItem existente (misma tabla que /inventory): QuantityOnHand =
// Quantity, LowStockThreshold = MinStock. Source viaja en snake_case: "donation_kind",
// "purchased_cash", "garden".

public record CommunityInventoryItemDto(
    Guid Id,
    string Name,
    string Unit,
    decimal QuantityOnHand,
    decimal UnitCost,
    DateOnly? ExpirationDate,
    string? Source,
    decimal? LowStockThreshold,
    bool IsLowStock,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record UpsertCommunityInventoryItemRequest(
    string Name,
    string? Unit,
    decimal QuantityOnHand,
    decimal UnitCost,
    DateOnly? ExpirationDate,
    string? Source,
    decimal? LowStockThreshold
);

public record RecipeIngredientDto(
    Guid Id,
    Guid InventoryItemId,
    string InventoryItemName,
    string Unit,
    decimal QuantityRequired,
    decimal UnitCost,
    decimal LineCost
);

public record RecipeDto(
    Guid Id,
    string Name,
    int Servings,
    decimal CostPerServing,
    IReadOnlyList<RecipeIngredientDto> Ingredients
);

// CostPerServing no se acepta: siempre se calcula server-side.
public record UpsertRecipeRequest(string Name, int Servings);

public record UpsertRecipeIngredientRequest(Guid InventoryItemId, decimal QuantityRequired);

public record ComboDto(
    Guid Id,
    string Name,
    IReadOnlyList<Guid> RecipeIds,
    decimal CostPerPlate
);

// CostPerPlate no se acepta: siempre se calcula server-side.
public record UpsertComboRequest(string Name, IReadOnlyList<Guid> RecipeIds);

public record ServeComboRequest(int Quantity);

public record InventoryConsumptionDto(
    Guid InventoryItemId,
    string Name,
    string Unit,
    decimal Consumed,
    decimal QuantityOnHand,
    // true = no alcanzaba: se descontó hasta 0 y faltó (Consumed - lo que había). El plato ya se
    // sirvió físicamente, así que no se rechaza — se reporta para que corrijan el inventario.
    bool Shortage
);

public record ServeComboResponse(
    Guid ServingId,
    Guid ComboId,
    int Quantity,
    decimal CostPerPlate,
    decimal TotalCost,
    DateTime ServedAt,
    IReadOnlyList<InventoryConsumptionDto> Consumption
);

// Dinero recaudado / causas activas quedan fuera hasta que existan Donation y Causa (Fase 3+):
// se omiten en vez de devolver 0 para que el dashboard no muestre un "$0 recaudado" falso.
public record CommunityMetricsDto(
    int MealsServedThisMonth,
    decimal MealsCostThisMonth,
    DateTime PeriodStart,
    DateTime PeriodEnd
);

// Vitrina pública (sin auth) — solo lo que es seguro mostrar a un visitante anónimo. No expone
// costos internos por insumo ni nombres de combos, solo el promedio necesario para la
// calculadora de impacto (WEB-COM-003). AvgCostPerPlate es null si el afiliado todavía no tiene
// ningún Combo con costo calculado — el frontend debe mostrar un estado vacío honesto, no 0.
public record PublicCommunityMetricsDto(
    int MealsServedThisMonth,
    decimal? AvgCostPerPlate
);
