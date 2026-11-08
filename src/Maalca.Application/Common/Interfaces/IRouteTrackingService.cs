using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>Modo camión: "llegué"/"salí" por parada y ubicación en vivo del teléfono del conductor.
/// ArgumentException = dato inválido; InvalidOperationException = conflicto de estado (ruta no en curso, parada ya atendida).
/// restrictDriverId = el Staff solo opera sus rutas (mismo criterio que IRouteService).</summary>
public interface IRouteTrackingService
{
    Task<RouteDto?> ArriveAsync(Guid affiliateId, Guid routeId, Guid stopId, ArriveStopRequest request, string? actorId, Guid? restrictDriverId = null);
    Task<RouteDto?> DepartAsync(Guid affiliateId, Guid routeId, Guid stopId, string? actorId, Guid? restrictDriverId = null);
    /// <summary>Guarda la última posición de la ruta. False si la ruta no existe o no es del conductor.</summary>
    Task<bool> PostPositionAsync(Guid affiliateId, Guid routeId, PositionRequest request, Guid? restrictDriverId = null);
    /// <summary>Rutas en curso con su última posición, para el dueño.</summary>
    Task<IReadOnlyList<LiveRouteDto>> GetLiveAsync(Guid affiliateId);
}
