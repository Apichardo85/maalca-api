namespace Maalca.Application.Common.DTOs;

/// <summary>
/// Miembro del equipo mostrado en el picker del kiosko público de ponche (/{slug}/ponche) —
/// deliberadamente sin PIN, email ni teléfono, mismo criterio que PublicTeamMemberDto (booking).
/// </summary>
public record PonchePickerMemberDto(Guid Id, string Name, string? PhotoUrl);

/// <summary>Body del POST de ponche público — el PIN es la única "autenticación".</summary>
public record ClockRequest(string Pin);

/// <summary>Resultado de un ponche (entrada o salida) para mostrarle confirmación al kiosko.</summary>
public record ClockResultDto(string Action, DateTime At, string TeamMemberName, decimal? HoursThisShift);

/// <summary>Body de corrección manual de un ponche (owner/manager, desde el dashboard).</summary>
public record UpdateTimeEntryRequest(DateTime ClockIn, DateTime? ClockOut, string? Notes);

/// <summary>Resumen de nómina de UN empleado en el rango de fechas consultado.</summary>
public record PayrollMemberDto(Guid TeamMemberId, string Name, decimal? HourlyRate, decimal TotalHours, decimal? TotalPay);

/// <summary>Reporte de nómina completo — lo que exporta el dueño para pagarle a su equipo.</summary>
public record PayrollReportDto(DateTime From, DateTime To, IReadOnlyList<PayrollMemberDto> Members, decimal GrandTotalPay);

/// <summary>Body para crear/editar una tarea asignada (PATCH completo — título/descripción/asignado/fecha).</summary>
public record StaffTaskRequest(string Title, string? Description, Guid? TeamMemberId, DateTime? DueDate);

/// <summary>Body del endpoint de solo-cambiar-estado (accesible también para rol Staff).</summary>
public record StaffTaskStatusRequest(string Status);
