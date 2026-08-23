namespace Maalca.Application.Common.DTOs;

/// <summary>
/// DTOs para el flujo público de reserva (sin login) — un cliente anónimo eligiendo un servicio,
/// opcionalmente un miembro del personal, y una fecha/hora. Deliberadamente NO reutiliza las
/// entidades TeamMember/Service/Appointment completas: esos tienen campos internos (email,
/// teléfono del staff, notas de otros clientes) que no deben salir por un endpoint anónimo.
/// </summary>
public record PublicTeamMemberDto(Guid Id, string Name, string Role, string? PhotoUrl = null);

// Modality: "InPerson" | "Virtual" | "Both" — ver Service.Modality. El front usa esto para saber
// si debe mostrar el selector Presencial/Virtual al reservar (solo cuando es "Both").
public record PublicServiceDto(Guid Id, string Name, string? Description, decimal Price, int DurationMinutes, string Modality = "InPerson");

public record CreatePublicAppointmentRequest(
    Guid ServiceId,
    Guid? AssignedToId,
    DateTime Date,
    string Time,
    string CustomerName,
    string CustomerPhone,
    string? Notes,
    // Tarea #247 — opcional a propósito (el flujo público nunca lo pidió, no queremos volverlo
    // obligatorio de golpe): si viene, se dispara el correo de confirmación con el link de
    // autogestión; si no, la cita se crea igual, solo sin correo.
    string? CustomerEmail = null,
    // Tarea #405 — solo relevante cuando el Service tiene Modality=Both (el cliente elige); si el
    // Service es InPerson o Virtual a secas, este valor se ignora y se fuerza server-side según
    // Service.Modality (ver PublicBookingService.CreatePublicAppointmentAsync).
    bool? WantsVirtual = null
);

// ZoomLink viaja solo cuando la cita quedó marcada IsVirtual=true, para que el front la muestre
// en la pantalla de confirmación sin un segundo round-trip.
public record PublicAppointmentResultDto(Guid Id, DateTime Date, string Time, string Status, Guid Token, bool IsVirtual = false, string? ZoomLink = null);

/// <summary>
/// Tarea #246 — vista pública de una cita por Token, para la página "gestiona tu cita"
/// (/cita/{token}), sin login. Proyectado a DTO por la misma razón que el resto de este
/// archivo: nunca exponer la entidad completa (Notes internas, IDs de otras tablas, etc.)
/// </summary>
public record PublicAppointmentManageDto(
    Guid Token,
    string BusinessName,
    string ServiceName,
    string? StaffName,
    DateTime Date,
    string Time,
    string Status,
    bool IsVirtual = false,
    string? ZoomLink = null
);

public record ReschedulePublicAppointmentRequest(DateTime Date, string Time);

/// <summary>
/// Horarios ya ocupados para una fecha dada, agrupados por miembro del personal (AssignedToId
/// como string porque el front lo usa como clave de mapa; "any" no existe acá — el front decide
/// cuándo tratar un slot como ocupado para la opción "cualquiera disponible" combinando todas las
/// listas). Ver task #189: antes el front no sabía nada de esto y el usuario descubría el
/// choque recién al confirmar (409).
/// </summary>
public record PublicBusyTimesDto(Dictionary<string, List<string>> BusyByStaff);

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

/// <summary>
/// Aceptación pública de una propuesta (task #194) — el cliente solo escribe su nombre, no es
/// una firma dibujada/certificada. Ver ProposalService.AcceptPublicProposalAsync.
/// </summary>
public record AcceptProposalRequest(string SignedByName);

/// <summary>
/// Walk-in "Ahora mismo" — sin fecha/hora, entra directo a la Fila (QueueEntry, no Appointment).
/// ServiceId es opcional a propósito: el cliente puede no saber qué corte quiere todavía.
/// </summary>
public record CreatePublicQueueEntryRequest(
    string CustomerName,
    string? CustomerPhone,
    Guid? ServiceId,
    string? Notes
);

public record PublicQueueEntryResultDto(Guid Id, int Position, string Status);
