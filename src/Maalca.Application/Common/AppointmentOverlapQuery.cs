using Maalca.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Common;

/// <summary>
/// El chequeo de choque de citas, en una sola implementación. Lo usan las tres rutas que ponen
/// o mueven una cita: la reserva pública, el reagendar por token (ambas en PublicBookingService)
/// y el alta desde el dashboard (AppointmentService). Antes cada una tenía su propia copia
/// comparando la hora como string, con tres criterios distintos para el caso "sin profesional".
///
/// Recibe el IQueryable en vez del DbContext para no acoplarse a un contexto concreto y para que
/// el llamador decida de dónde salen las citas.
/// </summary>
public static class AppointmentOverlapQuery
{
    /// <summary>
    /// ¿La cita que se quiere poner (fecha + hora + duración) se pisa con alguna ya existente en
    /// la misma línea? "Línea" = el mismo profesional, o la de las citas sin asignar cuando
    /// <paramref name="staffId"/> es null.
    ///
    /// Las filas del día se traen y se comparan en memoria: Time es un string "HH:mm" y la
    /// duración vive en Service, así que el intervalo no se calcula en SQL sin ensuciar el
    /// esquema. Son pocas citas por día y por línea.
    /// </summary>
    public static async Task<bool> HasOverlapAsync(
        IQueryable<Appointment> appointments,
        Guid affiliateId,
        Guid? staffId,
        DateTime dateUtc,
        string time,
        int? durationMinutes,
        Guid? excludeAppointmentId = null)
    {
        var query = appointments.Where(a =>
            a.AffiliateId == affiliateId &&
            a.Date.Date == dateUtc &&
            a.Status != "Cancelled");

        // Se ramifica en vez de comparar contra un Guid? capturado: EF compensa las semánticas
        // de null, pero el SQL que genera para eso es justo lo que nadie quiere depurar después.
        query = staffId is Guid sid
            ? query.Where(a => a.AssignedToId == sid)
            : query.Where(a => a.AssignedToId == null);

        if (excludeAppointmentId is Guid excludeId)
            query = query.Where(a => a.Id != excludeId);

        var sameLine = await query
            .Select(a => new { a.Time, Duration = a.Service!.DurationMinutes })
            .ToListAsync();

        return sameLine.Any(existing =>
            AppointmentSchedule.Overlaps(time, durationMinutes, existing.Time, existing.Duration));
    }
}
