namespace Maalca.Application.Common.DTOs;

public record AffiliatePublicDto(
    Guid Id,
    string Name,
    string Slug,
    string BusinessType,
    string? Description,
    string? PrimaryColor,
    string? LogoUrl,
    string? CoverImageUrl,
    string? WhatsApp,
    string? ContactEmail,
    string? Address,
    string? City,       // not on Affiliate entity yet — always null until migration adds it
    string? Website,
    List<CanalDto> Canales
);

public record CatalogItemDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string? Category,
    string? ImageUrl,
    int SortOrder,
    bool IsDemo,
    int? DurationMinutes,   // Service/Barber only
    int? Stock,             // InventoryItem only (null = no tracking)
    string? Status
);

public record PlanCapabilitiesDto(
    bool OnlinePayments,
    bool BookingCalendar,
    bool MenuModifiers,
    bool RealtimeStock,
    bool BrandingFull,
    bool HidePoweredBy,
    bool CustomDomain
);

public record PublicCatalogResponse(
    AffiliatePublicDto Affiliate,
    List<CatalogItemDto> Items,
    PlanCapabilitiesDto Capabilities
);
