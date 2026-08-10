namespace Maalca.Application.Common.DTOs;

public record ScreenAdDto(
    Guid Id,
    string MediaUrl,
    string MediaType,   // "Image" | "Video"
    int DurationSeconds,
    int SortOrder,
    bool Active,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string Fit = "Contain"   // "Contain" | "Cover" — cómo se ajusta dentro del recuadro
);

public record CreateScreenAdRequest(
    string MediaUrl,
    string MediaType,
    int DurationSeconds,
    int SortOrder,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string? Fit = null
);

public record UpdateScreenAdRequest(
    string? MediaUrl,
    string? MediaType,
    int? DurationSeconds,
    int? SortOrder,
    bool? Active,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string? Fit = null
);
