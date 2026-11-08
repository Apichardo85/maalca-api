using System.Globalization;
using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class ExpenseService : IExpenseService
{
    private static readonly string[] Categories =
        { "luz", "agua", "alquiler", "nomina", "combustible", "mantenimiento", "internet", "prestamos", "materia_prima", "impuestos", "otro" };
    private static readonly string[] Recurrences = { "none", "monthly" };
    private static readonly string[] Methods = { "cash", "transfer", "card" };

    private readonly AppDbContext _db;

    public ExpenseService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ExpenseDto>> GetAsync(Guid affiliateId, DateOnly? from, DateOnly? to, string? category)
    {
        var q = _db.Expenses.AsNoTracking().Where(e => e.AffiliateId == affiliateId && e.IsActive);
        if (from.HasValue) q = q.Where(e => e.ExpenseDate >= from.Value);
        if (to.HasValue) q = q.Where(e => e.ExpenseDate <= to.Value);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(e => e.Category == category);
        var list = await q.OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.CreatedAt).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<ExpenseDto> CreateAsync(Guid affiliateId, UpsertExpenseRequest request, string? actorId)
    {
        Validate(request);
        var expense = new Expense { AffiliateId = affiliateId, CreatedBy = actorId };
        Apply(expense, request);
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();
        return ToDto(expense);
    }

    public async Task<ExpenseDto?> UpdateAsync(Guid affiliateId, Guid expenseId, UpsertExpenseRequest request, string? actorId)
    {
        Validate(request);
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == expenseId && e.AffiliateId == affiliateId && e.IsActive);
        if (expense is null) return null;
        Apply(expense, request);
        expense.UpdatedAt = DateTime.UtcNow;
        expense.UpdatedBy = actorId;
        await _db.SaveChangesAsync();
        return ToDto(expense);
    }

    public async Task<bool> DeleteAsync(Guid affiliateId, Guid expenseId, string? actorId)
    {
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == expenseId && e.AffiliateId == affiliateId && e.IsActive);
        if (expense is null) return false;
        expense.IsActive = false;
        expense.UpdatedAt = DateTime.UtcNow;
        expense.UpdatedBy = actorId;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<GenerateRecurringResultDto> GenerateRecurringAsync(Guid affiliateId, GenerateRecurringRequest request, string? actorId)
    {
        var (year, month) = ParseMonth(request.Month);
        var first = new DateOnly(year, month, 1);
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        // Los originales son los gastos fijos sin SourceExpenseId; sus copias apuntan a ellos.
        var templates = await _db.Expenses.AsNoTracking()
            .Where(e => e.AffiliateId == affiliateId && e.IsActive && e.Recurrence == "monthly" && e.SourceExpenseId == null)
            .ToListAsync();
        var existing = await _db.Expenses.AsNoTracking()
            .Where(e => e.AffiliateId == affiliateId && e.IsActive && e.SourceExpenseId != null && e.ExpenseDate >= first && e.ExpenseDate <= last)
            .Select(e => e.SourceExpenseId!.Value)
            .ToListAsync();
        var have = existing.ToHashSet();

        var created = new List<Expense>();
        var already = 0;
        foreach (var t in templates)
        {
            // Solo meses posteriores al del original (el del original ya es el gasto cargado a mano).
            if (t.ExpenseDate >= first) { if (t.ExpenseDate <= last) already++; continue; }
            if (have.Contains(t.Id)) { already++; continue; }
            var day = Math.Min(t.ExpenseDate.Day, last.Day);
            created.Add(new Expense
            {
                AffiliateId = affiliateId,
                Category = t.Category,
                Description = t.Description,
                Amount = t.Amount,
                ExpenseDate = new DateOnly(year, month, day),
                Recurrence = "monthly",
                Vendor = t.Vendor,
                PaymentMethod = t.PaymentMethod,
                Notes = t.Notes,
                SourceExpenseId = t.Id,
                CreatedBy = actorId,
            });
        }
        if (created.Count > 0)
        {
            _db.Expenses.AddRange(created);
            await _db.SaveChangesAsync();
        }
        return new GenerateRecurringResultDto(request.Month, created.Select(ToDto).ToList(), already);
    }

    public async Task<ProfitReportDto> GetProfitAsync(Guid affiliateId, string month)
    {
        var (year, m) = ParseMonth(month);
        var first = new DateOnly(year, m, 1);
        var last = new DateOnly(year, m, DateTime.DaysInMonth(year, m));
        var (fromUtc, toUtc) = DeliveryInvoicing.MonthRangeUtc(year, m);

        // Facturas emitidas (reparto/contratos). Las anuladas no cuentan.
        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.AffiliateId == affiliateId && i.Status != "Cancelled" && i.VoidedAt == null && i.IssueDate >= fromUtc && i.IssueDate < toUtc)
            .Select(i => new { i.Subtotal, i.Tax }).ToListAsync();

        // Ventas de mostrador/tienda: pedidos pagados o entregados, sin la propina (no es ingreso del negocio).
        var counter = await _db.Orders.AsNoTracking()
            .Where(o => o.AffiliateId == affiliateId && o.CreatedAt >= fromUtc && o.CreatedAt < toUtc
                        && o.Status != OrderStatus.Pending && o.Status != OrderStatus.Canceled)
            .Select(o => o.Total - o.Tip).ToListAsync();

        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.AffiliateId == affiliateId && p.PaidAt >= fromUtc && p.PaidAt < toUtc)
            .Select(p => new { p.Method, p.Amount }).ToListAsync();

        var expenses = await _db.Expenses.AsNoTracking()
            .Where(e => e.AffiliateId == affiliateId && e.IsActive && e.ExpenseDate >= first && e.ExpenseDate <= last)
            .Select(e => new { e.Category, e.Amount }).ToListAsync();

        // El impuesto facturado (ITBIS) no es ingreso del negocio: las ventas cuentan la base sin impuesto.
        var salesInvoiced = Math.Round(invoices.Sum(i => i.Subtotal), 2);
        var taxInvoiced = Math.Round(invoices.Sum(i => i.Tax), 2);
        var salesCounter = Math.Round(counter.Sum(), 2);
        var cash = Math.Round(payments.Where(p => p.Method == "cash").Sum(p => p.Amount), 2);
        var transfer = Math.Round(payments.Where(p => p.Method != "cash").Sum(p => p.Amount), 2);
        var expensesTotal = Math.Round(expenses.Sum(e => e.Amount), 2);
        var byCategory = expenses.GroupBy(e => e.Category)
            .Select(g => new ExpenseCategoryTotalDto(g.Key, Math.Round(g.Sum(e => e.Amount), 2)))
            .OrderByDescending(c => c.Total).ToList();

        var salesTotal = salesInvoiced + salesCounter;
        var collectedTotal = cash + transfer + salesCounter;
        return new ProfitReportDto(
            month, first, last,
            salesInvoiced, invoices.Count, salesCounter, counter.Count, salesTotal,
            cash, transfer, salesCounter, collectedTotal,
            expensesTotal, byCategory,
            salesTotal - expensesTotal, collectedTotal - expensesTotal, taxInvoiced);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static (int Year, int Month) ParseMonth(string? month)
    {
        if (string.IsNullOrWhiteSpace(month)
            || !DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            throw new ArgumentException("El mes debe tener formato aaaa-mm (ej. 2026-11).");
        return (d.Year, d.Month);
    }

    private static void Validate(UpsertExpenseRequest r)
    {
        if (!Categories.Contains(r.Category)) throw new ArgumentException($"Categoría no válida. Usa: {string.Join(", ", Categories)}.");
        if (string.IsNullOrWhiteSpace(r.Description)) throw new ArgumentException("Escribe una descripción del gasto.");
        if (r.Description.Trim().Length > 200) throw new ArgumentException("La descripción es demasiado larga (máximo 200).");
        if (r.Amount <= 0) throw new ArgumentException("El monto debe ser mayor que cero.");
        if (!Recurrences.Contains(r.Recurrence)) throw new ArgumentException("Recurrencia no válida (none o monthly).");
        if (r.PaymentMethod is not null && !Methods.Contains(r.PaymentMethod)) throw new ArgumentException("Método de pago no válido (cash, transfer o card).");
    }

    private static void Apply(Expense e, UpsertExpenseRequest r)
    {
        e.Category = r.Category;
        e.Description = r.Description.Trim();
        e.Amount = Math.Round(r.Amount, 2);
        e.ExpenseDate = r.ExpenseDate;
        e.Recurrence = r.Recurrence;
        e.Vendor = string.IsNullOrWhiteSpace(r.Vendor) ? null : r.Vendor.Trim();
        e.PaymentMethod = string.IsNullOrWhiteSpace(r.PaymentMethod) ? null : r.PaymentMethod;
        e.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
    }

    private static ExpenseDto ToDto(Expense e) =>
        new(e.Id, e.Category, e.Description, e.Amount, e.ExpenseDate, e.Recurrence, e.Vendor, e.PaymentMethod, e.Notes, e.SourceExpenseId, e.IsActive);
}
