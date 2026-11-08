using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Gasto del negocio (luz, alquiler, nómina, combustible…). Alimenta la utilidad mensual (ventas − gastos).
/// Un gasto fijo se marca "monthly": el dueño lo carga una vez y cada mes se generan las copias (SourceExpenseId apunta al original).
/// "Borrar" = IsActive false: el gasto sale de los reportes pero queda en el historial.
/// </summary>
public class Expense : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    /// <summary>luz | agua | alquiler | nomina | combustible | mantenimiento | internet | prestamos | materia_prima | impuestos | otro.</summary>
    public string Category { get; set; } = "otro";
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    /// <summary>Fecha del gasto (día local del negocio).</summary>
    public DateOnly ExpenseDate { get; set; }
    /// <summary>"none" | "monthly".</summary>
    public string Recurrence { get; set; } = "none";
    public string? Vendor { get; set; }
    /// <summary>"cash" | "transfer" | "card" | null.</summary>
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    /// <summary>Gasto fijo original del que se generó esta copia mensual. Null en los originales y en los gastos sueltos.</summary>
    public Guid? SourceExpenseId { get; set; }
    public bool IsActive { get; set; } = true;

    public Affiliate? Affiliate { get; set; }
}
