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
    decimal Quantity,
    decimal MinStock,
    string Unit
);

// Created/Updated/Skipped: en DryRun (vista previa) son lo que PASARÍA al confirmar; no se escribe nada.
// Duplicates: filas del CSV cuyo nombre ya existe en el inventario (se avisa y el dueño decide si actualiza).
public record InventoryCsvImportResultDto(
    int Created,
    int ErrorCount,
    IReadOnlyList<string> Errors,
    int Updated = 0,
    int Skipped = 0,
    IReadOnlyList<InventoryCsvDuplicateDto>? Duplicates = null,
    bool DryRun = false
);

public record InventoryCsvDuplicateDto(
    int Row,
    string Name,
    decimal CurrentPrice,
    decimal? NewPrice
);

// Mode: "preview" (no escribe, solo informa duplicados), "skip" (crea los nuevos, deja los duplicados
// como están; es el valor por defecto) o "update" (crea los nuevos y actualiza los duplicados).
public record ImportInventoryCsvRequest(string CsvContent, string? Mode = null);
