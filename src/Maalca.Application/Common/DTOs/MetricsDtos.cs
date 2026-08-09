namespace Maalca.Application.Common.DTOs;

public record DetailedMetricsResponse(
    IReadOnlyList<DailyCountDto> DailyCounts,
    IReadOnlyList<CanalBreakdownDto> ByCanal,
    ConversionSummaryDto Conversion
);

public record DailyCountDto(
    string Date,
    int PageViews,
    int QrScans,
    int CanalClicks,
    int PaidOrders
);

/// <summary>
/// Visitas vs. pedidos reales en el mismo rango de días — el "para qué" detrás de las visitas
/// que ya se medían antes. ConversionRatePct viene precalculado (no crudo) porque dividir en el
/// frontend obliga a duplicar la regla de "0 si no hay visitas" en TS; mejor calcularla una vez
/// aquí. Revenue solo suma pedidos Paid (Pending/Canceled no son ventas reales todavía).
/// </summary>
public record ConversionSummaryDto(
    int Visits,
    int PaidOrders,
    decimal ConversionRatePct,
    decimal Revenue,
    string Currency
);

public record CanalBreakdownDto(
    Guid CanalId,
    string Tipo,
    string? NombreVisible,
    int Clicks
);
