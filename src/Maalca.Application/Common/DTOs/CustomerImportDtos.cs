namespace Maalca.Application.Common.DTOs;

/// <summary>Una fila ya mapeada desde la hoja de cálculo (el mapeo de columnas lo hace la web).
/// BillingCycle / DeliveryFrequency aceptan los valores del sistema (weekly, biweekly, monthly / daily, weekly, on_demand)
/// o su equivalente en español (quincenal, mensual, semanal, diario, por pedido).</summary>
public record ImportCustomerRow(
    string? Name, string? Phone, string? Email, string? Rnc, string? Address, string? Notes,
    string? BillingCycle, string? DeliveryFrequency, int? CreditDays);

/// <summary>DryRun = true (por defecto) solo valida y no escribe nada: es la vista previa antes de confirmar.</summary>
public record ImportCustomersRequest(IReadOnlyList<ImportCustomerRow> Rows, bool DryRun = true);

/// <summary>Status: new (se creará / se creó) | duplicate (ya existe o se repite en el archivo) | invalid.
/// Row = posición de la fila en la lista enviada, empezando en 1.</summary>
public record ImportCustomerRowResultDto(int Row, string Status, string? Name, string? Reason);

/// <summary>Imported = clientes realmente creados (0 en DryRun).</summary>
public record ImportCustomersResultDto(
    bool DryRun, int Total, int New, int Duplicates, int Invalid, int Imported,
    IReadOnlyList<ImportCustomerRowResultDto> Rows);
