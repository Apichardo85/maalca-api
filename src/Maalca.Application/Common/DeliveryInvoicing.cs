using System.Globalization;
using Maalca.Domain.Entities;

namespace Maalca.Application.Common;

/// <summary>
/// Reglas compartidas para facturar entregas: numeración, líneas, periodos de cobro y fecha local del negocio.
/// Las usan RouteService (factura inmediata de clientes de contado) y DeliveryBillingService (factura por ciclo).
/// </summary>
public static class DeliveryInvoicing
{
    // República Dominicana: UTC-4 todo el año (sin horario de verano). Si el servidor no conoce la zona, cae a UTC.
    private static readonly TimeZoneInfo BusinessZone = ResolveZone();

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "America/Santo_Domingo", "SA Western Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { } catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }

    public static DateOnly LocalDate(DateTime utc)
    {
        var u = utc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(utc, DateTimeKind.Utc) : utc.ToUniversalTime();
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(u, BusinessZone));
    }

    public static DateOnly Today() => LocalDate(DateTime.UtcNow);

    /// <summary>Mes calendario local del negocio como rango UTC [from, to): para filtrar columnas DateTime guardadas en UTC.</summary>
    public static (DateTime FromUtc, DateTime ToUtc) MonthRangeUtc(int year, int month)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var end = start.AddMonths(1);
        return (TimeZoneInfo.ConvertTimeToUtc(start, BusinessZone), TimeZoneInfo.ConvertTimeToUtc(end, BusinessZone));
    }

    /// <summary>Mismo formato que el resto de las facturas (INV-aaaammdd-xxxxxxxx).</summary>
    public static string NewInvoiceNumber() => $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";

    /// <summary>
    /// Último día del periodo de cobro que contiene la fecha: semanal = hasta el domingo, quincenal = hasta el 15 o fin de mes,
    /// mensual (o sin ciclo definido) = fin de mes.
    /// </summary>
    public static DateOnly CycleEnd(DateOnly date, string? billingCycle)
    {
        var endOfMonth = new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
        return billingCycle switch
        {
            "weekly" => date.AddDays((7 - (int)date.DayOfWeek) % 7),
            "biweekly" => date.Day <= 15 ? new DateOnly(date.Year, date.Month, 15) : endOfMonth,
            _ => endOfMonth,
        };
    }

    /// <summary>Junta las líneas iguales (mismo producto y precio) de varias entregas en una sola línea de factura.</summary>
    public static List<InvoiceItem> BuildLines(IEnumerable<DeliveryItem> items)
    {
        var lines = new List<InvoiceItem>();
        foreach (var g in items.GroupBy(i => (i.ItemId, Desc: i.Description.Trim(), i.UnitPrice)))
        {
            var qty = g.Sum(i => i.Quantity);
            if (qty <= 0) continue;
            var price = g.Key.UnitPrice;
            if (qty == decimal.Truncate(qty) && qty <= int.MaxValue)
            {
                lines.Add(new InvoiceItem
                {
                    Id = Guid.NewGuid(),
                    Description = g.Key.Desc,
                    Quantity = (int)qty,
                    UnitPrice = price,
                    Total = Math.Round(qty * price, 2, MidpointRounding.AwayFromZero),
                });
            }
            else
            {
                // La factura guarda cantidades enteras: una cantidad con decimales va como una línea de total exacto.
                var total = Math.Round(qty * price, 2, MidpointRounding.AwayFromZero);
                lines.Add(new InvoiceItem
                {
                    Id = Guid.NewGuid(),
                    Description = $"{g.Key.Desc} ({qty.ToString("0.##", CultureInfo.InvariantCulture)} × {price.ToString("0.##", CultureInfo.InvariantCulture)})",
                    Quantity = 1,
                    UnitPrice = total,
                    Total = total,
                });
            }
        }
        return lines;
    }
}
