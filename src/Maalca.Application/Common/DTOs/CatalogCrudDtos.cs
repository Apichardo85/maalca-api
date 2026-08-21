namespace Maalca.Application.Common.DTOs;

public record CreateCatalogItemRequest(
    string Name,
    string? Description,
    decimal Price,
    string? Category,
    string? ImageUrl,
    int SortOrder,
    int? DurationMinutes,
    int? Stock,
    string? DescriptionEn = null,               // Product/Service/InventoryItem
    IReadOnlyList<string>? Periods = null,      // Product only
    IReadOnlyList<string>? WeekDays = null,     // Product only
    IReadOnlyList<string>? Flags = null,        // Product only
    bool? Featured = null,                      // Product only
    bool? Popular = null,                       // Product only
    string? VideoUrl = null,                    // Product only — Menu Board Fase 9 Etapa A
    IReadOnlyList<string>? Images = null        // Galería completa (orden = orden de visualización); Images[0] pasa a ser ImageUrl
);

public record UpdateCatalogItemRequest(
    string? Name,
    string? Description,
    decimal? Price,
    string? Category,
    string? ImageUrl,
    int? SortOrder,
    bool? IsPubliclyVisible,
    int? DurationMinutes,
    int? Stock,
    string? Status,          // "Active" | "Inactive"
    string? DescriptionEn = null,               // Product/Service/InventoryItem
    IReadOnlyList<string>? Periods = null,      // Product only
    IReadOnlyList<string>? WeekDays = null,     // Product only
    IReadOnlyList<string>? Flags = null,        // Product only
    bool? Featured = null,                      // Product only
    bool? Popular = null,                       // Product only
    string? VideoUrl = null,                    // Product only — Menu Board Fase 9 Etapa A
    IReadOnlyList<string>? Images = null        // null = no tocar la galería; [] = vaciarla; lista = reemplazarla entera
);

// Receta (ProductIngredient) — Product (plato) -> InventoryItem (ingrediente) + cantidad consumida
// por unidad vendida. InventoryItemName/Unit viajan de vuelta solo para pintar la UI sin un
// segundo round-trip; en el PUT solo se leen InventoryItemId/Quantity.
public record RecipeItemDto(
    Guid InventoryItemId,
    string InventoryItemName,
    decimal Quantity
);

public record SetRecipeRequest(
    IReadOnlyList<RecipeItemInput> Items
);

public record RecipeItemInput(
    Guid InventoryItemId,
    decimal Quantity
);
