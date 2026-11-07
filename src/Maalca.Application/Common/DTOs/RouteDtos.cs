namespace Maalca.Application.Common.DTOs;

public record VehicleDto(
    Guid Id, string Plate, string? Name, int CapacityContainers,
    Guid? DriverId, string? DriverName, string Status, bool IsActive, string? Notes);

public record UpsertVehicleRequest(
    string Plate, string? Name = null, int CapacityContainers = 0,
    Guid? DriverId = null, string? Status = null, bool IsActive = true, string? Notes = null);

public record RouteStopDto(
    Guid Id, Guid CustomerId, string CustomerName, string? CustomerPhone, string? CustomerAddress,
    int SortOrder, string Status, DateTime? VisitedAt, string? Notes);

public record RouteDto(
    Guid Id, string Name, DateOnly RouteDate,
    Guid? VehicleId, string? VehiclePlate, Guid? DriverId, string? DriverName,
    string Status, int? LoadedFull, DateTime? DepartedAt, DateTime? ClosedAt, string? Notes,
    int StopsTotal, int StopsDone,
    IReadOnlyList<RouteStopDto> Stops);

/// <summary>CustomerIds = paradas en el orden de visita. En una actualización reemplaza las paradas (solo si la ruta aún no salió).</summary>
public record UpsertRouteRequest(
    string Name, DateOnly RouteDate,
    Guid? VehicleId = null, Guid? DriverId = null, string? Notes = null,
    IReadOnlyList<Guid>? CustomerIds = null);

public record StartRouteRequest(int? LoadedFull = null);

public record CloseRouteRequest(bool MarkPendingAsSkipped = false, string? Notes = null);

/// <summary>Status: "skipped" (no se visitó) o "failed" (se intentó y no se pudo: cerrado, no estaba, etc.).</summary>
public record SkipStopRequest(string Status = "skipped", string? Notes = null);

public record DeliveryContainerLine(Guid ContainerTypeId, int Delivered = 0, int Returned = 0, int Damaged = 0);

public record DeliveryItemInput(Guid? ItemId, string Description, decimal Quantity, decimal UnitPrice);

public record RegisterDeliveryRequest(
    IReadOnlyList<DeliveryItemInput>? Items = null,
    IReadOnlyList<DeliveryContainerLine>? Containers = null,
    Guid? AnaquelId = null,
    decimal? PaymentAmount = null,
    string? PaymentMethod = null,
    string? ReceivedBy = null,
    string? Notes = null,
    DateTime? DeliveredAt = null,
    string? IdempotencyKey = null);

public record DeliveryItemDto(Guid? ItemId, string Description, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public record DeliveryDto(
    Guid Id, Guid RouteStopId, Guid CustomerId, DateTime DeliveredAt,
    IReadOnlyList<DeliveryItemDto> Items, decimal Total,
    Guid? PaymentId, decimal? PaymentAmount, string? ReceivedBy, string? Notes);

public record RegisterDeliveryResultDto(DeliveryDto Delivery, bool Duplicate, CustomerContainerBalanceDto? Balance);

/// <summary>Cuadre de la ruta: lo cargado contra lo entregado, lo recogido y lo cobrado.</summary>
public record RouteReconciliationDto(
    Guid RouteId, string Status, int? LoadedFull,
    int DeliveredFull, int ReturnedEmpty, int Damaged,
    /// <summary>Llenos que deben volver a planta: cargados − entregados. Null si no se registró la carga.</summary>
    int? FullRemaining,
    int StopsTotal, int StopsVisited, int StopsSkipped, int StopsFailed, int StopsPending,
    decimal SalesTotal, decimal CollectedCash, decimal CollectedTransfer);
