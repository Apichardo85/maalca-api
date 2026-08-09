namespace Maalca.Application.Common.DTOs;

public record ScreenAdDto(
    Guid Id,
    string MediaUrl,
    string MediaType,   // "Image" | "Video"
    int DurationSeconds,
    int SortOrder,
    bool Active,
    DateTime? StartsAt,
    DateTime? EndsAt
);

public record CreateScreenAdRequest(
    string MediaUrl,
    string MediaType,
    int DurationSeconds,
    int SortOrder,
    DateTime? StartsAt,
    DateTime? EndsAt
);

public record UpdateScreenAdRequest(
    string? MediaUrl,
    string? MediaType,
    int? DurationSeconds,
    int? SortOrder,
    bool? Active,
    DateTime? StartsAt,
    DateTime? EndsAt
);
