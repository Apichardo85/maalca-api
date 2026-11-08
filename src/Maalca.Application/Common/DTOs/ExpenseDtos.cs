namespace Maalca.Application.Common.DTOs;

public record ExpenseDto(
    Guid Id, string Category, string Description, decimal Amount, DateOnly ExpenseDate,
    string Recurrence, string? Vendor, string? PaymentMethod, string? Notes, Guid? SourceExpenseId, bool IsActive);

public record UpsertExpenseRequest(
    string Category, string Description, decimal Amount, DateOnly ExpenseDate,
    string Recurrence = "none", string? Vendor = null, string? PaymentMethod = null, string? Notes = null);

/// <summary>Month = "yyyy-MM". Crea, para ese mes, la copia de cada gasto fijo mensual que aún no la tenga (no duplica).</summary>
public record GenerateRecurringRequest(string Month);

public record GenerateRecurringResultDto(string Month, IReadOnlyList<ExpenseDto> Created, int AlreadyExisting);

public record ExpenseCategoryTotalDto(string Category, decimal Total);

/// <summary>Utilidad del mes. Ventas = facturas emitidas (reparto/contratos) + ventas de mostrador/tienda (sin propina).
/// Utilidad = ventas − gastos (base devengado); UtilidadCaja = lo cobrado − gastos. El cobrado de facturas viene de los pagos registrados.</summary>
public record ProfitReportDto(
    string Month, DateOnly From, DateOnly To,
    decimal SalesInvoiced, int InvoicesCount, decimal SalesCounter, int CounterOrdersCount, decimal SalesTotal,
    decimal CollectedCash, decimal CollectedTransfer, decimal CollectedCounter, decimal CollectedTotal,
    decimal ExpensesTotal, IReadOnlyList<ExpenseCategoryTotalDto> ExpensesByCategory,
    decimal Profit, decimal CashProfit,
    /// <summary>Impuesto (ITBIS) de las facturas del mes: se cobra a nombre del fisco, no entra en ventas ni en utilidad.</summary>
    decimal TaxInvoiced = 0m);
