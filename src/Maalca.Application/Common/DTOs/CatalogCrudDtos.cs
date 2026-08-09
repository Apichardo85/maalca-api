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
    string? VideoUrl = null                     // Product only — Menu Board Fase 9 Etapa A
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
    string? VideoUrl = null                     // Product only — Menu Board Fase 9 Etapa A
);
