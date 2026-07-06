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
    List<CanalDto> Canales,
    List<ProcessStepDto> ProcessSteps,
    List<FaqItemDto> Faq
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
    string? Status,
    string? DescriptionEn = null,               // Product only
    IReadOnlyList<string>? Periods = null,      // Product only — breakfast/lunch/dinner/late_night/all_day
    IReadOnlyList<string>? WeekDays = null,     // Product only — monday..sunday
    IReadOnlyList<string>? Flags = null,        // Product only — free tokens, e.g. vegetarian/spicy/glutenFree
    bool? Featured = null,                      // Product only
    bool? Popular = null                        // Product only
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
