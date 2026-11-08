namespace Maalca.Application.Common.DTOs;

/// <summary>Posición opcional al marcar "llegué": sirve para validar que el conductor estaba en el sitio.</summary>
public record ArriveStopRequest(decimal? Lat = null, decimal? Lng = null);

public record PositionRequest(decimal Lat, decimal Lng, double? AccuracyMeters = null, double? SpeedKmh = null);

/// <summary>Una ruta en curso para el tablero del dueño. SecondsSincePosition null = el teléfono aún no ha enviado ubicación.</summary>
public record LiveRouteDto(
    Guid RouteId, string Name, string? VehiclePlate, string? DriverName,
    int StopsTotal, int StopsDone,
    Guid? CurrentStopId, string? CurrentStopCustomer, DateTime? CurrentStopArrivedAt,
    decimal? Lat, decimal? Lng, DateTime? PositionAt, int? SecondsSincePosition);
