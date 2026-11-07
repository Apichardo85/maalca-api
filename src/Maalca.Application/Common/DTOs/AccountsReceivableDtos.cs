namespace Maalca.Application.Common.DTOs;

/// <summary>Saldo abierto repartido por antigüedad (días desde la emisión de la factura).</summary>
public record AgingBucketsDto(decimal Days0To15, decimal Days16To30, decimal Days31To60, decimal Over60)
{
    public decimal Total => Days0To15 + Days16To30 + Days31To60 + Over60;
}

/// <summary>Factura con saldo pendiente (ya descontados pagos ligados y abonos a cuenta).</summary>
public record OpenInvoiceDto(
    Guid InvoiceId, string InvoiceNumber, DateTime IssueDate, DateTime? DueDate,
    decimal Open, int AgeDays, bool IsOverdue);

/// <summary>Movimiento del estado de cuenta. Type: invoice | payment | settled (factura marcada pagada a mano).</summary>
public record StatementLineDto(
    DateTime Date, string Type, Guid? InvoiceId, string? Reference, string? Description,
    decimal Debit, decimal Credit, decimal RunningBalance);

/// <summary>Estado de cuenta de un cliente. Balance = lo que debe (nunca negativo); CreditOnAccount = abonos que sobran.</summary>
public record CustomerStatementDto(
    Guid CustomerId, string CustomerName, DateTime AsOf,
    decimal TotalInvoiced, decimal TotalPaid, decimal Balance, decimal CreditOnAccount,
    AgingBucketsDto Aging, IReadOnlyList<OpenInvoiceDto> OpenInvoices, IReadOnlyList<StatementLineDto> Lines);

public record AgingRowDto(
    Guid CustomerId, string CustomerName, decimal Balance, decimal CreditOnAccount,
    AgingBucketsDto Aging, DateTime? OldestOpenInvoiceDate, bool HasOverdue);

/// <summary>Antigüedad de saldos de todos los clientes con deuda o crédito a favor.</summary>
public record AgingReportDto(
    DateTime AsOf, decimal TotalBalance, decimal TotalCreditOnAccount,
    AgingBucketsDto Totals, IReadOnlyList<AgingRowDto> Rows);
