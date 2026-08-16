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

/// <summary>
/// Reserva de mesa pública — deliberadamente distinta de CreatePublicAppointmentRequest: no pide
/// ServiceId ni AssignedToId, pide cuántas personas. Ver TableReservation.cs.
/// </summary>
public record CreatePublicTableReservationRequest(
    DateTime Date,
    string Time,
    int PartySize,
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail,
    string? Notes
);

public record PublicTableReservationResultDto(Guid Id, DateTime Date, string Time, int PartySize, string Status);
