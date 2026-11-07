using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common;

public record BillingInvoice(
    Guid Id, string InvoiceNumber, decimal Total, string Status,
    DateTime IssueDate, DateTime? DueDate, DateTime? PaidDate, DateTime CreatedAt);

public record BillingPayment(Guid Id, Guid? InvoiceId, decimal Amount, DateTime PaidAt, string Method, string? Notes);

public record CustomerBillingResult(
    decimal TotalInvoiced, decimal TotalPaid, decimal Balance, decimal CreditOnAccount,
    AgingBucketsDto Aging, IReadOnlyList<OpenInvoiceDto> OpenInvoices, IReadOnlyList<StatementLineDto> Lines);

/// <summary>
/// Matemática de cobros de UN cliente, sin base de datos (para poder probarla con escenarios).
///
/// Reglas:
///  - Facturas "Cancelled" no cuentan. Una factura "Paid" sin pagos en el libro (el botón "Marcar pagada" no crea
///    Payment) cuenta como cobrada por su total: sale como línea "settled" en el estado de cuenta.
///  - Un pago ligado a una factura la abona. Un pago sin factura (InvoiceId null), el sobrante de una factura
///    pagada de más y los pagos de facturas anuladas forman un "crédito a cuenta".
///  - El crédito a cuenta se aplica a las facturas abiertas de la más vieja a la más nueva (FIFO).
///  - Antigüedad = días desde la fecha de emisión: 0–15, 16–30, 31–60, 61+.
///  - Identidad: TotalInvoiced − TotalPaid = Balance − CreditOnAccount.
/// </summary>
public static class BillingCalculator
{
    public const string Cancelled = "Cancelled";
    public const string Paid = "Paid";

    public static CustomerBillingResult Compute(
        IEnumerable<BillingInvoice> invoices, IEnumerable<BillingPayment> payments, DateTime asOf)
    {
        var asOfDate = asOf.Date;
        var live = invoices
            .Where(i => i.Status != Cancelled && i.IssueDate <= asOf)
            .OrderBy(i => i.IssueDate).ThenBy(i => i.CreatedAt)
            .ToList();
        var pays = payments.Where(p => p.PaidAt <= asOf).ToList();

        var liveIds = live.Select(i => i.Id).ToHashSet();
        var numberById = live.ToDictionary(i => i.Id, i => i.InvoiceNumber);
        var linked = pays
            .Where(p => p.InvoiceId.HasValue && liveIds.Contains(p.InvoiceId.Value))
            .GroupBy(p => p.InvoiceId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        // Crédito a cuenta: pagos sin factura o ligados a una factura que ya no cuenta.
        var creditPool = pays
            .Where(p => !p.InvoiceId.HasValue || !liveIds.Contains(p.InvoiceId.Value))
            .Sum(p => p.Amount);

        var lines = new List<(DateTime Date, int Order, string Type, Guid? InvoiceId, string? Reference, string? Description, decimal Debit, decimal Credit)>();
        var open = new List<(BillingInvoice Inv, decimal Amount)>();
        decimal totalInvoiced = 0, settledExtra = 0;

        foreach (var inv in live)
        {
            totalInvoiced += inv.Total;
            lines.Add((inv.IssueDate, 0, "invoice", inv.Id, inv.InvoiceNumber, null, inv.Total, 0m));

            linked.TryGetValue(inv.Id, out var linkedAmount);
            var settled = inv.Status == Paid && (inv.PaidDate ?? inv.IssueDate) <= asOf;
            var diff = inv.Total - linkedAmount;

            if (settled)
            {
                if (diff > 0)
                {
                    settledExtra += diff;
                    lines.Add((inv.PaidDate ?? inv.IssueDate, 2, "settled", inv.Id, inv.InvoiceNumber, "Marcada como pagada", 0m, diff));
                }
                else if (diff < 0) creditPool += -diff;
            }
            else
            {
                if (diff > 0) open.Add((inv, diff));
                else if (diff < 0) creditPool += -diff;
            }
        }

        foreach (var p in pays)
        {
            string? reference = null;
            if (p.InvoiceId.HasValue) numberById.TryGetValue(p.InvoiceId.Value, out reference);
            lines.Add((p.PaidAt, 1, "payment", p.InvoiceId, reference, p.Notes, 0m, p.Amount));
        }

        // FIFO: el crédito a cuenta cubre primero las facturas más viejas.
        decimal remainingCredit = creditPool;
        var openInvoices = new List<OpenInvoiceDto>();
        decimal b0 = 0, b1 = 0, b2 = 0, b3 = 0;
        foreach (var (inv, amount) in open)
        {
            var applied = Math.Min(remainingCredit, amount);
            remainingCredit -= applied;
            var left = amount - applied;
            if (left <= 0) continue;

            var age = (asOfDate - inv.IssueDate.Date).Days;
            if (age <= 15) b0 += left;
            else if (age <= 30) b1 += left;
            else if (age <= 60) b2 += left;
            else b3 += left;

            var overdue = inv.DueDate.HasValue && inv.DueDate.Value.Date < asOfDate;
            openInvoices.Add(new OpenInvoiceDto(inv.Id, inv.InvoiceNumber, inv.IssueDate, inv.DueDate, R(left), age, overdue));
        }

        decimal running = 0;
        var statement = lines
            .OrderBy(l => l.Date).ThenBy(l => l.Order)
            .Select(l =>
            {
                running += l.Debit - l.Credit;
                return new StatementLineDto(l.Date, l.Type, l.InvoiceId, l.Reference, l.Description, R(l.Debit), R(l.Credit), R(running));
            })
            .ToList();

        var totalPaid = pays.Sum(p => p.Amount) + settledExtra;
        var aging = new AgingBucketsDto(R(b0), R(b1), R(b2), R(b3));
        return new CustomerBillingResult(R(totalInvoiced), R(totalPaid), R(openInvoices.Sum(o => o.Open)), R(remainingCredit), aging, openInvoices, statement);
    }

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
