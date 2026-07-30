namespace Maalca.Application.Common.DTOs;

public record DetailedMetricsResponse(
    IReadOnlyList<DailyCountDto> DailyCounts,
    IReadOnlyList<CanalBreakdownDto> ByCanal
);

public record DailyCountDto(
    string Date,
    int PageViews,
    int QrScans,
    int CanalClicks
);

public record CanalBreakdownDto(
    Guid CanalId,
    string Tipo,
    string? NombreVisible,
    int Clicks
);
