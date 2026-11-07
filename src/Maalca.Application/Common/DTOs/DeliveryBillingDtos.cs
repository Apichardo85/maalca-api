namespace Maalca.Application.Common.DTOs;

/// <summary>Entregas con productos que todavía no tienen factura, por cliente. Ready = su periodo de cobro ya terminó.</summary>
public record UnbilledCustomerDto(
    Guid CustomerId, string CustomerName, string? BillingCycle, int? CreditDays,
    int Deliveries, decimal Total, DateOnly OldestDelivery,
    /// <summary>Último día del periodo de cobro de la entrega más vieja.</summary>
    DateOnly CycleEnd, bool Ready, int ReadyDeliveries, decimal ReadyTotal);

public record UnbilledReportDto(DateOnly AsOf, IReadOnlyList<UnbilledCustomerDto> Customers, decimal TotalUnbilled, decimal TotalReady);

/// <summary>CustomerIds null = todos los que ya cerraron su periodo. ThroughDate null = hasta el cierre del periodo de cada cliente
/// (o hasta hoy si lo eligió el dueño y su periodo aún no cierra).
/// DryRun true (por defecto) solo muestra lo que se facturaría, sin crear nada.</summary>
public record BillDeliveriesRequest(IReadOnlyList<Guid>? CustomerIds = null, DateOnly? ThroughDate = null, bool DryRun = true);

/// <summary>Status: "invoiced" (se creó la factura) | "preview" (simulación) | "empty" (sin entregas facturables) | "zero_total".</summary>
public record BillDeliveriesRowDto(
    Guid CustomerId, string CustomerName, string Status, int Deliveries, decimal Total,
    DateOnly ThroughDate, DateTime? DueDate, Guid? InvoiceId, string? InvoiceNumber);

public record BillDeliveriesResultDto(bool DryRun, IReadOnlyList<BillDeliveriesRowDto> Rows, int InvoicesCreated, decimal TotalInvoiced);
