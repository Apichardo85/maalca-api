using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>Vehículos, rutas de reparto y entregas. ArgumentException = dato inválido; InvalidOperationException = conflicto
/// de estado (ej. la ruta ya salió). Las rutas y vehículos no se borran: se cancelan o se desactivan.</summary>
public interface IRouteService
{
    Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(Guid affiliateId, bool includeInactive);
    Task<VehicleDto> CreateVehicleAsync(Guid affiliateId, UpsertVehicleRequest request, string? actorId);
    Task<VehicleDto?> UpdateVehicleAsync(Guid affiliateId, Guid vehicleId, UpsertVehicleRequest request, string? actorId);

    /// <summary>Rutas fijas. Las plantillas no se borran: se desactivan.</summary>
    Task<IReadOnlyList<RouteTemplateDto>> GetTemplatesAsync(Guid affiliateId, bool includeInactive);
    Task<RouteTemplateDto> CreateTemplateAsync(Guid affiliateId, UpsertRouteTemplateRequest request, string? actorId);
    Task<RouteTemplateDto?> UpdateTemplateAsync(Guid affiliateId, Guid templateId, UpsertRouteTemplateRequest request, string? actorId);

    Task<IReadOnlyList<RouteDto>> GetRoutesAsync(Guid affiliateId, DateOnly? from, DateOnly? to, string? status, Guid? restrictDriverId = null);
    Task<RouteDto?> GetRouteAsync(Guid affiliateId, Guid routeId, Guid? restrictDriverId = null);
    Task<RouteDto> CreateRouteAsync(Guid affiliateId, UpsertRouteRequest request, string? actorId);
    Task<RouteDto?> UpdateRouteAsync(Guid affiliateId, Guid routeId, UpsertRouteRequest request, string? actorId);
    Task<RouteDto?> StartRouteAsync(Guid affiliateId, Guid routeId, StartRouteRequest request, string? actorId, Guid? restrictDriverId = null);
    Task<RouteDto?> CancelRouteAsync(Guid affiliateId, Guid routeId, string? actorId);
    Task<RouteReconciliationDto?> CloseRouteAsync(Guid affiliateId, Guid routeId, CloseRouteRequest request, string? actorId, Guid? restrictDriverId = null);
    Task<RouteReconciliationDto?> GetReconciliationAsync(Guid affiliateId, Guid routeId, Guid? restrictDriverId = null);

    Task<RegisterDeliveryResultDto?> RegisterDeliveryAsync(Guid affiliateId, Guid routeId, Guid stopId, RegisterDeliveryRequest request, string? actorId, string? actorName, Guid? restrictDriverId = null);
    Task<RouteDto?> SkipStopAsync(Guid affiliateId, Guid routeId, Guid stopId, SkipStopRequest request, string? actorId, Guid? restrictDriverId = null);

    /// <summary>Conductor (ficha de Equipo) que corresponde a un correo, o null. Un usuario Staff solo opera las rutas donde es el conductor.</summary>
    Task<Guid?> FindDriverIdByEmailAsync(Guid affiliateId, string? email);
    /// <summary>Rutas de un conductor para una fecha (por defecto hoy) que aún no cerraron.</summary>
    Task<IReadOnlyList<RouteDto>> GetMyRoutesAsync(Guid affiliateId, Guid driverId, DateOnly? date);
    /// <summary>Paquete completo de una ruta para trabajarla desde el celular. Null si no existe o no es del conductor.</summary>
    Task<DriverSheetDto?> GetDriverSheetAsync(Guid affiliateId, Guid routeId, Guid? restrictDriverId = null);
}
