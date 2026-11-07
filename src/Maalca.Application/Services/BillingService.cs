using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

/// <summary>Lee facturas y pagos del negocio y delega toda la matemática en BillingCalculator.</summary>
public class BillingService : IBillingService
{
    private readonly AppDbContext _db;

    public BillingService(AppDbContext db) => _db = db;

    public async Task<CustomerStatementDto?> GetStatementAsync(Guid affiliateId, Guid customerId, DateTime? asOf = null)
    {
        var at = asOf ?? DateTime.UtcNow;
        var customer = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == customerId && c.AffiliateId == affiliateId)
            .Select(c => new { c.Id, c.Name })
            .FirstOrDefaultAsync();
        if (customer is null) return null;

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.AffiliateId == affiliateId && i.CustomerId == customerId && i.VoidedAt == null)
            .Select(i => new { i.Id, i.InvoiceNumber, i.Total, i.Status, i.IssueDate, i.DueDate, i.PaidDate, i.CreatedAt })
            .ToListAsync();
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.AffiliateId == affiliateId && p.CustomerId == customerId)
            .Select(p => new { p.Id, p.InvoiceId, p.Amount, p.PaidAt, p.Method, p.Notes })
            .ToListAsync();

        var result = BillingCalculator.Compute(
            invoices.Select(i => new BillingInvoice(i.Id, i.InvoiceNumber, i.Total, i.Status, i.IssueDate, i.DueDate, i.PaidDate, i.CreatedAt)),
            payments.Select(p => new BillingPayment(p.Id, p.InvoiceId, p.Amount, p.PaidAt, p.Method, p.Notes)),
            at);

        return new CustomerStatementDto(
            customer.Id, customer.Name, at, result.TotalInvoiced, result.TotalPaid, result.Balance,
            result.CreditOnAccount, result.Aging, result.OpenInvoices, result.Lines);
    }

    public async Task<AgingReportDto> GetAgingAsync(Guid affiliateId, DateTime? asOf = null)
    {
        var at = asOf ?? DateTime.UtcNow;

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.AffiliateId == affiliateId && i.VoidedAt == null)
            .Select(i => new { i.CustomerId, i.Id, i.InvoiceNumber, i.Total, i.Status, i.IssueDate, i.DueDate, i.PaidDate, i.CreatedAt })
            .ToListAsync();
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.AffiliateId == affiliateId)
            .Select(p => new { p.CustomerId, p.Id, p.InvoiceId, p.Amount, p.PaidAt, p.Method, p.Notes })
            .ToListAsync();

        var invoicesByCustomer = invoices.ToLookup(i => i.CustomerId);
        var paymentsByCustomer = payments.ToLookup(p => p.CustomerId);
        var customerIds = invoicesByCustomer.Select(g => g.Key).Union(paymentsByCustomer.Select(g => g.Key)).ToList();

        var names = await _db.Customers.AsNoTracking()
            .Where(c => c.AffiliateId == affiliateId && customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name);

        var rows = new List<AgingRowDto>();
        foreach (var id in customerIds)
        {
            var r = BillingCalculator.Compute(
                invoicesByCustomer[id].Select(i => new BillingInvoice(i.Id, i.InvoiceNumber, i.Total, i.Status, i.IssueDate, i.DueDate, i.PaidDate, i.CreatedAt)),
                paymentsByCustomer[id].Select(p => new BillingPayment(p.Id, p.InvoiceId, p.Amount, p.PaidAt, p.Method, p.Notes)),
                at);
            if (r.Balance <= 0 && r.CreditOnAccount <= 0) continue;

            rows.Add(new AgingRowDto(
                id, names.TryGetValue(id, out var n) ? n : "—", r.Balance, r.CreditOnAccount, r.Aging,
                r.OpenInvoices.Count > 0 ? r.OpenInvoices.Min(o => o.IssueDate) : null,
                r.OpenInvoices.Any(o => o.IsOverdue)));
        }

        rows = rows.OrderByDescending(r => r.Balance).ThenBy(r => r.CustomerName).ToList();
        var totals = new AgingBucketsDto(
            rows.Sum(r => r.Aging.Days0To15), rows.Sum(r => r.Aging.Days16To30),
            rows.Sum(r => r.Aging.Days31To60), rows.Sum(r => r.Aging.Over60));
        return new AgingReportDto(at, rows.Sum(r => r.Balance), rows.Sum(r => r.CreditOnAccount), totals, rows);
    }
}
