namespace Maalca.Application.Common.DTOs;

public record SpaceResponse(
    BusinessDto Business,
    IReadOnlyList<SpaceItemDto> Items,
    int ProductCount,
    ProgressDto Progress,
    KpisDto Kpis,
    string Role,
    // Sesión de soporte (impersonation, ver PlatformAdminService.StartImpersonationAsync) —
    // el frontend usa esto para mostrar el banner "modo soporte" y el botón de salida real
    // dentro de /space, en vez de obligar a volver a /ops escribiendo la URL a mano.
    bool IsImpersonation = false,
    DateTime? ImpersonationExpiresAt = null
);

public record BusinessDto(
    Guid Id,
    string Slug,
    string Name,
    string BusinessType,
    string Plan,
    string PlanStatus,
    string? Whatsapp,
    string? PrimaryColor,
    string? LogoUrl,
    string? DescriptionEn,
    IReadOnlyList<CanalDto> Canales,
    IReadOnlyList<string> ModulosActivos,
    IReadOnlyList<ProcessStepDto> ProcessSteps,
    IReadOnlyList<FaqItemDto> Faq,
    IReadOnlyList<HorarioEntryDto> Horario,
    string? Timezone,
    int? TrialDaysRemaining,
    DateTime? TrialEndsAt,
    string Currency = "USD",
    string? ZoomLink = null,
    string? SecondaryColor = null,
    string? AccentColor = null,
    IReadOnlyDictionary<string, MealPeriodRangeDto>? MealPeriodHours = null,
    IReadOnlyDictionary<string, CategoryTranslationDto>? CategoryTranslations = null,
    // Idioma principal del negocio (Affiliate.Language) y bandera junto a "ES" (Settings.spanishFlag).
    string Language = "es",
    string? SpanishFlag = null
);

public record ProcessStepDto(string Title, string Description);

public record FaqItemDto(string Question, string Answer);

public record HorarioEntryDto(string Dia, string Abre, string Cierra, bool Cerrado);

public record SpaceItemDto(
    Guid Id,
    string Name,
    string? Category,
    bool IsDemo,
    bool Active,
    string? ImageUrl,
    string? Description = null,
    IReadOnlyList<string>? Periods = null,      // Product only
    IReadOnlyList<string>? Flags = null,        // Product only
    bool? Featured = null,                      // Product only
    bool? Popular = null,                       // Product only
    decimal? Price = null,
    IReadOnlyList<string>? WeekDays = null,     // Product only
    string? NameEn = null,                      // Product only
    string? DescriptionEn = null                // Product only
);

public record ProgressDto(
    bool FirstProductAdded,
    bool CanalesConfigured,
    bool LinkShared
);
