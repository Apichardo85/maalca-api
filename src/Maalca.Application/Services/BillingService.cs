using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

/// <summary>Lee facturas y pagos del negocio y delega toda la matemática en BillingCalculator.</summary>
public class BillingService : IBillingService
{
    private readonly AppDbContext _db;
    private readonly IAuditLogService _audit;

    public BillingService(AppDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

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

    // ── Pagos ────────────────────────────────────────────────────────────────

    public async Task<RegisterPaymentResultDto> RegisterPaymentAsync(
        Guid affiliateId, RegisterPaymentRequest request, string? actorId, string? actorName)
    {
        var amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0) throw new ArgumentException("El monto debe ser mayor que cero.");

        var method = (request.Method ?? "cash").Trim().ToLowerInvariant();
        if (method is not ("cash" or "transfer"))
            throw new ArgumentException("Método de pago inválido (cash o transfer).");

        var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
        if (key is { Length: > 100 }) throw new ArgumentException("La clave de idempotencia es demasiado larga (máx. 100).");

        // Reintento con la misma clave → devuelve el pago original, no crea otro.
        if (key is not null)
        {
            var existing = await _db.Payments.AsNoTracking()
                .FirstOrDefaultAsync(p => p.AffiliateId == affiliateId && p.IdempotencyKey == key);
            if (existing is not null) return new RegisterPaymentResultDto(await ToDtoAsync(existing), true, false);
        }

        var customerExists = await _db.Customers.AnyAsync(c => c.Id == request.CustomerId && c.AffiliateId == affiliateId);
        if (!customerExists) throw new ArgumentException("El cliente no existe en este negocio.");

        Invoice? invoice = null;
        if (request.InvoiceId.HasValue)
        {
            invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId.Value && i.AffiliateId == affiliateId);
            if (invoice is null || invoice.CustomerId != request.CustomerId)
                throw new ArgumentException("La factura no existe o no es de este cliente.");
            if (invoice.Status == "Cancelled" || invoice.VoidedAt != null)
                throw new InvalidOperationException("La factura está anulada; no se le pueden registrar pagos.");
            if (invoice.Status == "Paid")
                throw new InvalidOperationException("La factura ya está pagada.");
        }

        var now = DateTime.UtcNow;
        var paidAt = request.PaidAt.HasValue ? DateTime.SpecifyKind(request.PaidAt.Value, DateTimeKind.Utc) : now;
        if (paidAt > now.AddMinutes(5)) throw new ArgumentException("La fecha del pago no puede ser futura.");

        var payment = new Payment
        {
            AffiliateId = affiliateId,
            CustomerId = request.CustomerId,
            InvoiceId = invoice?.Id,
            Amount = amount,
            Method = method,
            PaidAt = paidAt,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            RegisteredBy = actorName ?? actorId,
            IdempotencyKey = key,
        };
        _db.Payments.Add(payment);

        // Factura ligada: si lo ya abonado + este pago cubre el total, queda pagada.
        var markedPaid = false;
        if (invoice is not null)
        {
            var alreadyLinked = await _db.Payments.Where(p => p.InvoiceId == invoice.Id).SumAsync(p => p.Amount);
            if (alreadyLinked + amount >= invoice.Total)
            {
                invoice.Status = "Paid";
                invoice.PaidDate = paidAt;
                invoice.UpdatedAt = now;
                markedPaid = true;
            }
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (key is not null)
        {
            // Dos reintentos simultáneos con la misma clave: el índice único dejó pasar solo uno. Devuelve el ganador.
            _db.ChangeTracker.Clear();
            var winner = await _db.Payments.AsNoTracking()
                .FirstOrDefaultAsync(p => p.AffiliateId == affiliateId && p.IdempotencyKey == key);
            if (winner is not null) return new RegisterPaymentResultDto(await ToDtoAsync(winner), true, false);
            throw;
        }

        await _audit.LogAsync(affiliateId, "payment.registered", "Payment", payment.Id,
            invoice is null
                ? $"Abono a cuenta de {amount:N2} ({method})"
                : $"Pago de {amount:N2} ({method}) a la factura {invoice.InvoiceNumber}",
            actorId, actorName);
        if (markedPaid && invoice is not null)
            await _audit.LogAsync(affiliateId, "invoice.paid", "Invoice", invoice.Id,
                $"Factura {invoice.InvoiceNumber} marcada como pagada por cobro registrado ({invoice.Total:N2})", actorId, actorName);

        return new RegisterPaymentResultDto(ToDto(payment, invoice?.InvoiceNumber), false, markedPaid);
    }

    public async Task<IReadOnlyList<PaymentDto>> GetPaymentsAsync(Guid affiliateId, Guid customerId, int take = 100)
    {
        take = Math.Clamp(take, 1, 500);
        return await _db.Payments.AsNoTracking()
            .Where(p => p.AffiliateId == affiliateId && p.CustomerId == customerId)
            .OrderByDescending(p => p.PaidAt).ThenByDescending(p => p.CreatedAt)
            .Take(take)
            .Select(p => new PaymentDto(
                p.Id, p.CustomerId, p.InvoiceId, p.Invoice != null ? p.Invoice.InvoiceNumber : null,
                p.Amount, p.Method, p.PaidAt, p.Notes, p.RegisteredBy, p.CreatedAt))
            .ToListAsync();
    }

    private async Task<PaymentDto> ToDtoAsync(Payment p)
    {
        string? number = null;
        if (p.InvoiceId.HasValue)
            number = await _db.Invoices.AsNoTracking().Where(i => i.Id == p.InvoiceId.Value).Select(i => i.InvoiceNumber).FirstOrDefaultAsync();
        return ToDto(p, number);
    }

    private static PaymentDto ToDto(Payment p, string? invoiceNumber) => new(
        p.Id, p.CustomerId, p.InvoiceId, invoiceNumber, p.Amount, p.Method, p.PaidAt, p.Notes, p.RegisteredBy, p.CreatedAt);
}
