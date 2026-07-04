namespace Maalca.Application.Common.DTOs;

// Disponible=false means no real events have ever been recorded for this KPI — Valor is null,
// never 0, so the frontend can render "Próximamente" instead of a misleading zero.
public record KpiValueDto(int? Valor, bool Disponible);

public record KpisDto(
    KpiValueDto Visitas,
    KpiValueDto ItemsPublicados,
    KpiValueDto EscaneosQr,
    KpiValueDto ClicsCanales
);
