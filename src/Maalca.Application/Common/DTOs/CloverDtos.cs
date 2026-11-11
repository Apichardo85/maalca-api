namespace Maalca.Application.Common.DTOs;

/// <summary>Estado de la conexión con Clover. El token nunca sale del servidor: solo Connected + últimos 4.</summary>
public record CloverConnectionDto(bool Connected, string? MerchantId, string? MerchantName, string? TokenLast4, string Status, string? LastError, DateTime? LastSyncAt);

/// <summary>Token vacío = conservar el guardado (solo cambia el Merchant ID).</summary>
public record ConnectCloverRequest(string MerchantId, string? Token);

public record CloverDayDto(string Date, decimal Total, int Orders);
public record CloverTenderDto(string Label, decimal Total, int Count);
public record CloverTopItemDto(string Name, int Qty, decimal Total);

/// <summary>Ventas del local leídas de Clover: pagos exitosos del rango, en la zona horaria del negocio.</summary>
public record CloverSalesReportDto(
    string From, string To, int Orders, decimal Gross, decimal Tax, decimal Tips, decimal AvgTicket,
    List<CloverDayDto> ByDay, List<CloverTenderDto> ByTender, List<CloverTopItemDto> TopItems, bool Truncated);
