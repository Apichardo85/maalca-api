using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common;

/// <summary>
/// "¿Está abierto el negocio ahora?" — mismo cálculo que getOpenStatus() de la web
/// (src/lib/business-hours.ts), evaluado en la zona horaria IANA del NEGOCIO. Es la validación
/// de respaldo del servidor: la web ya bloquea el pedido antes de enviarlo, esto cubre a quien
/// tiene la página abierta desde hace horas o llama al API directo.
/// </summary>
public static class BusinessHoursCheck
{
    private static readonly string[] DayTokens =
        { "lunes", "martes", "miercoles", "jueves", "viernes", "sabado", "domingo" };

    /// <param name="Known">false = no se puede afirmar nada (sin Horario, sin zona válida o ningún turno válido): no se restringe.</param>
    /// <param name="NextOpenDate">Fecha local de la próxima apertura (puede ser hoy si abre más tarde).</param>
    public sealed record Result(bool Known, bool IsOpen, DateOnly? NextOpenDate, string? NextOpensAt, string? NextDayToken, int? DaysAhead = null);

    private static readonly Result Unknown = new(false, true, null, null, null);

    public static Result Evaluate(IReadOnlyList<HorarioEntryDto> horario, string? ianaTz, DateTime utcNow)
    {
        if (horario.Count == 0 || string.IsNullOrWhiteSpace(ianaTz)) return Unknown;

        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(ianaTz); }
        catch (Exception) { return Unknown; }

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);
        var minutes = local.Hour * 60 + local.Minute;
        var idx = ((int)local.DayOfWeek + 6) % 7; // lunes = 0
        var today = DateOnly.FromDateTime(local);

        (int Open, int Close)? ShiftFor(int dayIndex)
        {
            var token = DayTokens[((dayIndex % 7) + 7) % 7];
            var entry = horario.FirstOrDefault(h => string.Equals(h.Dia, token, StringComparison.OrdinalIgnoreCase));
            if (entry is null || entry.Cerrado) return null;
            var open = ToMinutes(entry.Abre);
            var close = ToMinutes(entry.Cierra);
            if (open is null || close is null || open == close) return null;
            return (open.Value, close.Value);
        }

        if (!Enumerable.Range(0, 7).Any(d => ShiftFor(d) is not null)) return Unknown;

        // 1) Sigue abierto por el turno de ayer que cruzó la medianoche.
        var yesterday = ShiftFor(idx - 1);
        if (yesterday is { } y && y.Close < y.Open && minutes < y.Close)
            return new Result(true, true, null, null, null);

        // 2) Turno de hoy.
        var shift = ShiftFor(idx);
        if (shift is { } t)
        {
            var crosses = t.Close < t.Open;
            var isOpen = crosses ? minutes >= t.Open : minutes >= t.Open && minutes < t.Close;
            if (isOpen) return new Result(true, true, null, null, null);
            if (minutes < t.Open)
                return new Result(true, false, today, ToHHmm(t.Open), DayTokens[idx], 0);
        }

        // 3) Próxima apertura en los siguientes 7 días.
        for (var ahead = 1; ahead <= 7; ahead++)
        {
            var next = ShiftFor(idx + ahead);
            if (next is { } n)
                return new Result(true, false, today.AddDays(ahead), ToHHmm(n.Open), DayTokens[(idx + ahead) % 7], ahead);
        }
        return new Result(true, false, null, null, null);
    }

    private static int? ToMinutes(string? hhmm)
    {
        var parts = (hhmm ?? "").Trim().Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)) return null;
        return (h is < 0 or > 23) || (m is < 0 or > 59) ? null : h * 60 + m;
    }

    private static string ToHHmm(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";
}
