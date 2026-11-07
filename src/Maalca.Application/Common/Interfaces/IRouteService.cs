using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>Vehículos, rutas de reparto y entregas. ArgumentException = dato inválido; InvalidOperationException = conflicto
/// de estado (ej. la ruta ya salió). Las rutas y vehículos no se borran: se cancelan o se desactivan.</summary>
public interface IRouteService
{
    Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(Guid affiliateId, bool includeInactive);
    Task<VehicleDto> CreateVehicleAsync(Guid affiliateId, UpsertVehicleRequest request, string? actorId);
    Task<VehicleDto?> UpdateVehicleAsync(Guid affiliateId, Guid vehicleId, UpsertVehicleRequest request, string? actorId);

    Task<IReadOnlyList<RouteDto>> GetRoutesAsync(Guid affiliateId, DateOnly? from, DateOnly? to, string? status);
    Task<RouteDto?> GetRouteAsync(Guid affiliateId, Guid routeId);
    Task<RouteDto> CreateRouteAsync(Guid affiliateId, UpsertRouteRequest request, string? actorId);
    Task<RouteDto?> UpdateRouteAsync(Guid affiliateId, Guid routeId, UpsertRouteRequest request, string? actorId);
    Task<RouteDto?> StartRouteAsync(Guid affiliateId, Guid routeId, StartRouteRequest request, string? actorId);
    Task<RouteDto?> CancelRouteAsync(Guid affiliateId, Guid routeId, string? actorId);
    Task<RouteReconciliationDto?> CloseRouteAsync(Guid affiliateId, Guid routeId, CloseRouteRequest request, string? actorId);
    Task<RouteReconciliationDto?> GetReconciliationAsync(Guid affiliateId, Guid routeId);

    Task<RegisterDeliveryResultDto?> RegisterDeliveryAsync(Guid affiliateId, Guid routeId, Guid stopId, RegisterDeliveryRequest request, string? actorId, string? actorName);
    Task<RouteDto?> SkipStopAsync(Guid affiliateId, Guid routeId, Guid stopId, SkipStopRequest request, string? actorId);
}
