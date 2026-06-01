namespace Maalca.Application.Common.DTOs;

public record SpaceResponse(
    BusinessDto Business,
    IReadOnlyList<SpaceItemDto> Items,
    int ProductCount,
    ProgressDto Progress
);

public record BusinessDto(
    Guid Id,
    string Slug,
    string Name,
    string BusinessType,
    string Plan,
    string? Whatsapp,
    string? PrimaryColor
);

public record SpaceItemDto(
    Guid Id,
    string Name,
    string? Category,
    bool IsDemo,
    bool Active,
    string? ImageUrl
);

public record ProgressDto(
    bool FirstProductAdded,
    bool WhatsAppConfigured,
    bool LinkShared
);
