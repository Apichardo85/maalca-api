namespace Maalca.Application.Common.DTOs;

// Resumen de Inventario — valor total y stock bajo, usado por la pantalla de Inventario y por
// la alerta del Dashboard del negocio (antes el stock bajo solo se veía entrando a Inventario).
public record InventorySummaryDto(
    decimal TotalValue,
    int TotalItems,
    int LowStockCount,
    IReadOnlyList<LowStockItemDto> LowStockItems
);

public record LowStockItemDto(
    Guid Id,
    string Name,
    int Quantity,
    int MinStock,
    string Unit
);

public record InventoryCsvImportResultDto(
    int Created,
    int ErrorCount,
    IReadOnlyList<string> Errors
);

public record ImportInventoryCsvRequest(string CsvContent);
