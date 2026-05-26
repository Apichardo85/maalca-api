namespace Maalca.Application.Common.DTOs;

public record CreateCatalogItemRequest(
    string Name,
    string? Description,
    decimal Price,
    string? Category,
    string? ImageUrl,
    int SortOrder,
    int? DurationMinutes,
    int? Stock
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
    int? Stock
);
