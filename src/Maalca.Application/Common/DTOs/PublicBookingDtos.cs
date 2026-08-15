namespace Maalca.Application.Common.DTOs;

/// <summary>
/// DTOs para el flujo público de reserva (sin login) — un cliente anónimo eligiendo un servicio,
/// opcionalmente un miembro del personal, y una fecha/hora. Deliberadamente NO reutiliza las
/// entidades TeamMember/Service/Appointment completas: esos tienen campos internos (email,
/// teléfono del staff, notas de otros clientes) que no deben salir por un endpoint anónimo.
/// </summary>
public record PublicTeamMemberDto(Guid Id, string Name, string Role, string? PhotoUrl = null);

public record PublicServiceDto(Guid Id, string Name, string? Description, decimal Price, int DurationMinutes);

public record CreatePublicAppointmentRequest(
    Guid ServiceId,
    Guid? AssignedToId,
    DateTime Date,
    string Time,
    string CustomerName,
    string CustomerPhone,
    string? Notes
);

public record PublicAppointmentResultDto(Guid Id, DateTime Date, string Time, string Status);
