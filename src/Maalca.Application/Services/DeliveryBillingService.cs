using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class DeliveryBillingService : IDeliveryBillingService
{
    private readonly AppDbContext _db;
    private readonly IAuditLogService _audit;

    public DeliveryBillingService(AppDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<UnbilledReportDto> GetUnbilledAsync(Guid affiliateId)
    {
        var today = DeliveryInvoicing.Today();
        var (deliveries, customers) = await LoadUnbilledAsync(affiliateId, null);

        var rows = new List<UnbilledCustomerDto>();
        foreach (var g in deliveries.GroupBy(d => d.CustomerId))
        {
            if (!customers.TryGetValue(g.Key, out var c)) continue;
            var dated = g.Select(d => (Date: DeliveryInvoicing.LocalDate(d.DeliveredAt), Total: Total(d))).ToList();
            var oldest = dated.Min(x => x.Date);
            var cutoff = DeliveryInvoicing.CycleEnd(oldest, c.BillingCycle);
            var ready = cutoff < today;
            var inCycle = dated.Where(x => x.Date <= cutoff).ToList();
            rows.Add(new UnbilledCustomerDto(
                c.Id, c.Name, c.BillingCycle, c.CreditDays,
                dated.Count, dated.Sum(x => x.Total), oldest, cutoff, ready,
                ready ? inCycle.Count : 0, ready ? inCycle.Sum(x => x.Total) : 0m));
        }

        var ordered = rows.OrderByDescending(r => r.Ready).ThenBy(r => r.CustomerName).ToList();
        return new UnbilledReportDto(today, ordered, ordered.Sum(r => r.Total), ordered.Sum(r => r.ReadyTotal));
    }

    public async Task<BillDeliveriesResultDto> BillAsync(Guid affiliateId, BillDeliveriesRequest request, string? actorId, string? actorName)
    {
        if (request.CustomerIds is { Count: > 500 })
            throw new ArgumentException("Máximo 500 clientes por corrida.");
        var today = DeliveryInvoicing.Today();
        if (request.ThroughDate.HasValue && request.ThroughDate.Value > today)
            throw new ArgumentException("La fecha de corte no puede ser futura.");

        // Facturar de verdad: una sola corrida a la vez por negocio. El candado se toma antes de leer las entregas sin factura,
        // así una segunda corrida simultánea (doble clic, dos pestañas) espera, las ve ya facturadas y no las factura otra vez.
        await using var tx = request.DryRun ? null : await _db.Database.BeginTransactionAsync();
        if (tx is not null)
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BillingLockKey(affiliateId)})");

        var (deliveries, customers) = await LoadUnbilledAsync(affiliateId, request.CustomerIds, track: !request.DryRun);

        var rows = new List<BillDeliveriesRowDto>();
        var created = new List<(Invoice Invoice, string CustomerName, int Count)>();
        var now = DateTime.UtcNow;

        foreach (var g in deliveries.GroupBy(d => d.CustomerId))
        {
            if (!customers.TryGetValue(g.Key, out var c)) continue;
            var oldest = g.Min(d => DeliveryInvoicing.LocalDate(d.DeliveredAt));
            var cycleEnd = DeliveryInvoicing.CycleEnd(oldest, c.BillingCycle);

            // Sin lista de clientes ni fecha de corte, solo entran los que ya cerraron su periodo.
            // Con una lista, o con fecha de corte, el dueño decide y entran todos los que tengan entregas.
            if (request.CustomerIds is null && !request.ThroughDate.HasValue && cycleEnd >= today)
                continue;
            var through = request.ThroughDate ?? (cycleEnd < today ? cycleEnd : today);

            var billable = g.Where(d => DeliveryInvoicing.LocalDate(d.DeliveredAt) <= through).ToList();
            if (billable.Count == 0)
            {
                rows.Add(new BillDeliveriesRowDto(c.Id, c.Name, "empty", 0, 0m, through, null, null, null));
                continue;
            }

            var lines = DeliveryInvoicing.BuildLines(billable.SelectMany(d => d.Items));
            var subtotal = lines.Sum(l => l.Total);
            DateTime? due = c.CreditDays.HasValue ? now.AddDays(c.CreditDays.Value) : null;
            if (subtotal <= 0)
            {
                rows.Add(new BillDeliveriesRowDto(c.Id, c.Name, "zero_total", billable.Count, 0m, through, due, null, null));
                continue;
            }

            if (request.DryRun)
            {
                rows.Add(new BillDeliveriesRowDto(c.Id, c.Name, "preview", billable.Count, subtotal, through, due, null, null));
                continue;
            }

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                AffiliateId = affiliateId,
                CustomerId = c.Id,
                InvoiceNumber = DeliveryInvoicing.NewInvoiceNumber(),
                Subtotal = subtotal,
                Tax = 0m,
                Total = subtotal,
                Status = "Pending",
                IssueDate = now,
                DueDate = due,
                Notes = $"Entregas hasta el {through:dd/MM/yyyy} ({billable.Count} {(billable.Count == 1 ? "entrega" : "entregas")})",
                CreatedAt = now,
                CreatedBy = actorId,
                Items = lines,
            };
            _db.Invoices.Add(invoice);
            foreach (var d in billable) d.InvoiceId = invoice.Id;
            created.Add((invoice, c.Name, billable.Count));
            rows.Add(new BillDeliveriesRowDto(c.Id, c.Name, "invoiced", billable.Count, subtotal, through, due, invoice.Id, invoice.InvoiceNumber));
        }

        if (created.Count > 0) await _db.SaveChangesAsync();
        if (tx is not null) await tx.CommitAsync();

        foreach (var (invoice, name, count) in created)
        {
            await _audit.LogAsync(affiliateId, "invoice.created", "Invoice", invoice.Id,
                $"Factura {invoice.InvoiceNumber} de {name} por {invoice.Total:N2}: {count} entregas del reparto",
                actorId, actorName);
        }

        return new BillDeliveriesResultDto(request.DryRun, rows, created.Count, created.Sum(x => x.Invoice.Total));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>Llave del candado de facturación por negocio (primeros 8 bytes del id).</summary>
    private static long BillingLockKey(Guid affiliateId) => BitConverter.ToInt64(affiliateId.ToByteArray(), 0);

    private static decimal Total(Delivery d) => d.Items.Sum(i => Math.Round(i.Quantity * i.UnitPrice, 2, MidpointRounding.AwayFromZero));

    /// <summary>Entregas sin factura y con al menos un producto, y los clientes a los que pertenecen.</summary>
    private async Task<(List<Delivery> deliveries, Dictionary<Guid, Customer> customers)> LoadUnbilledAsync(
        Guid affiliateId, IReadOnlyList<Guid>? customerIds, bool track = false)
    {
        var q = _db.Deliveries.Include(d => d.Items)
            .Where(d => d.AffiliateId == affiliateId && d.InvoiceId == null && d.Items.Any());
        if (customerIds is not null) q = q.Where(d => customerIds.Contains(d.CustomerId));
        if (!track) q = q.AsNoTracking();
        var deliveries = await q.OrderBy(d => d.DeliveredAt).Take(20_000).ToListAsync();

        var ids = deliveries.Select(d => d.CustomerId).Distinct().ToList();
        var customers = await _db.Customers.AsNoTracking()
            .Where(c => c.AffiliateId == affiliateId && ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);
        return (deliveries, customers);
    }
}
