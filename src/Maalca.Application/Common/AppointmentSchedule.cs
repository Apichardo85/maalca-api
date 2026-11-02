namespace Maalca.Application.Common;

/// <summary>
/// Reglas de horario de la agenda, en un solo lugar.
///
/// Por qué existe: el chequeo de choque comparaba la hora exacta como string
/// (<c>a.Time == request.Time</c>) y nadie miraba la duración del servicio. Con la grilla de 30
/// minutos del front, una asesoría de 60 min a las 09:00 no bloqueaba las 09:30: dos clientes
/// quedaban solapados media hora, y en modalidad virtual eso es la misma sala. El criterio
/// correcto es intervalo contra intervalo, y vive acá porque lo necesitan las tres rutas que
/// crean citas (reserva pública, reagendar por token, y el alta desde el dashboard).
/// </summary>
public static class AppointmentSchedule
{
    /// <summary>Paso de la grilla de horarios — mismo que generateTimeSlots() en PublicBookingSection.</summary>
    public const int SlotMinutes = 30;

    /// <summary>
    /// Service.DurationMinutes es nullable (null = el dueño no fijó duración y el ítem se
    /// oculta en catálogo). Mismo fallback que GetPublicServicesAsync, para que la agenda
    /// calcule igual en los dos lados.
    /// </summary>
    public const int DefaultDurationMinutes = 30;

    /// <summary>"HH:mm" → minutos desde medianoche. False si no parsea.</summary>
    public static bool TryParseHm(string? value, out int minutes)
    {
        minutes = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split(':');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)) return false;
        if (h < 0 || h > 23 || m < 0 || m > 59) return false;
        minutes = h * 60 + m;
        return true;
    }

    /// <summary>Minutos desde medianoche → "HH:mm".</summary>
    public static string FormatHm(int minutes) => $"{minutes / 60:D2}:{minutes % 60:D2}";

    /// <summary>Duración efectiva: la del servicio si la hay, si no el fallback.</summary>
    public static int NormalizeDuration(int? durationMinutes) =>
        durationMinutes is int d && d > 0 ? d : DefaultDurationMinutes;

    /// <summary>
    /// ¿Se pisan [startA, startA+durA) y [startB, startB+durB)? Fin exclusivo a propósito: una
    /// cita de 09:00 a 10:00 y otra que empieza 10:00 en punto no chocan.
    /// </summary>
    public static bool Overlaps(int startA, int durationA, int startB, int durationB) =>
        startA < startB + durationB && startB < startA + durationA;

    /// <summary>Igual que el anterior pero sobre "HH:mm" y duraciones nullable. Si alguna hora no parsea, no se asume choque.</summary>
    public static bool Overlaps(string? timeA, int? durationA, string? timeB, int? durationB)
    {
        if (!TryParseHm(timeA, out var a) || !TryParseHm(timeB, out var b)) return false;
        return Overlaps(a, NormalizeDuration(durationA), b, NormalizeDuration(durationB));
    }

    /// <summary>
    /// Todos los slots de la grilla que una cita ocupa, para que el front los tache. Una cita de
    /// 60 min a las 09:00 devuelve ["09:00", "09:30"]; una de 45 min devuelve lo mismo, porque el
    /// slot de 09:30 queda parcialmente ocupado y no se puede vender.
    /// </summary>
    public static List<string> CoveredSlots(string? startTime, int? durationMinutes)
    {
        var slots = new List<string>();
        if (!TryParseHm(startTime, out var start)) return slots;

        var end = start + NormalizeDuration(durationMinutes);
        // Alineado a la grilla hacia abajo: una cita a las 09:10 (creada a mano desde el panel,
        // fuera de la grilla) ocupa el slot de 09:00.
        for (var cursor = start - (start % SlotMinutes); cursor < end; cursor += SlotMinutes)
            slots.Add(FormatHm(cursor));

        return slots;
    }

    /// <summary>
    /// SEQUENCE de RFC 5545 para el .ics de la cita. Con el mismo UID, el calendario del cliente
    /// solo acepta una actualización si el número sube; cualquier constante (0 al reservar, 1 al
    /// confirmar) se rompe en cuanto hay una segunda vuelta — reagendar y volver a confirmar
    /// repetiría el 1 y el cambio se ignoraría. Se usa el instante de última modificación en
    /// segundos Unix: siempre creciente, sin columna nueva. Cabe en int32 hasta 2038.
    /// </summary>
    public static int RevisionStamp(DateTime createdAt, DateTime? updatedAt) =>
        (int)new DateTimeOffset(DateTime.SpecifyKind(updatedAt ?? createdAt, DateTimeKind.Utc)).ToUnixTimeSeconds();
}
