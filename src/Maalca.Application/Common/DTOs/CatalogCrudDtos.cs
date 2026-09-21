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
    IReadOnlyList<string>? Images = null,       // Galería completa (orden = orden de visualización); Images[0] pasa a ser ImageUrl
    string? NameEn = null,                      // Product/Service/InventoryItem — nombre en inglés, fallback a Name si null
    string? Modality = null                     // Service only — "InPerson" | "Virtual" | "Both". Null = InPerson (default del enum).
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
    IReadOnlyList<string>? Images = null,       // null = no tocar la galería; [] = vaciarla; lista = reemplazarla entera
    string? NameEn = null,                      // Product/Service/InventoryItem — nombre en inglés, fallback a Name si null
    string? Modality = null                     // Service only — "InPerson" | "Virtual" | "Both". Null = no tocar.
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

// Grupos de modificadores reutilizables (ej. "Guarnición") — afiliado-scoped, se enlazan a
// Product vía ProductModifierGroup. Asignar/desasignar grupos a un producto es reemplazo total
// por PUT (SetProductModifierGroupsRequest), mismo patrón que SetRecipeRequest.
public record ModifierOptionDto(
    Guid Id,
    string Name,
    string? NameEn,
    decimal PriceDelta,
    bool IsDefault,
    int SortOrder
);

public record ModifierGroupDto(
    Guid Id,
    string Name,
    string? NameEn,
    int MinSelect,
    int MaxSelect,
    bool Required,
    int SortOrder,
    IReadOnlyList<ModifierOptionDto> Options
);

public record ModifierOptionInput(
    Guid? Id,           // null = opción nueva; ignorado en el server (siempre se regenera el Id)
    string Name,
    string? NameEn,
    decimal PriceDelta,
    bool IsDefault,
    int SortOrder
);

public record CreateModifierGroupRequest(
    string Name,
    string? NameEn,
    int MinSelect,
    int MaxSelect,
    bool Required,
    int SortOrder,
    IReadOnlyList<ModifierOptionInput> Options
);

public record UpdateModifierGroupRequest(
    string? Name,
    string? NameEn,
    int? MinSelect,
    int? MaxSelect,
    bool? Required,
    int? SortOrder,
    IReadOnlyList<ModifierOptionInput>? Options   // null = no tocar las opciones; lista = reemplazarlas por completo
);

public record SetProductModifierGroupsRequest(
    IReadOnlyList<Guid> ModifierGroupIds
);
