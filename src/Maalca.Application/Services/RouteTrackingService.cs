using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class RouteTrackingService : IRouteTrackingService
{
    private readonly AppDbContext _db;
    private readonly IRouteService _routes;

    public RouteTrackingService(AppDbContext db, IRouteService routes)
    {
        _db = db;
        _routes = routes;
    }

    public async Task<RouteDto?> ArriveAsync(Guid affiliateId, Guid routeId, Guid stopId, ArriveStopRequest request, string? actorId, Guid? restrictDriverId = null)
    {
        ValidateCoords(request.Lat, request.Lng);
        var route = await LoadAsync(affiliateId, routeId, restrictDriverId);
        var stop = route?.Stops.FirstOrDefault(s => s.Id == stopId);
        if (route is null || stop is null) return null;
        if (route.Status != "in_progress") throw new InvalidOperationException("La ruta no está en curso.");
        if (stop.Status != "pending") throw new InvalidOperationException("Esa parada ya fue atendida.");

        // Idempotente: un segundo toque (o un reintento sin señal) no pisa la hora real de llegada.
        if (stop.ArrivedAt is null)
        {
            stop.ArrivedAt = DateTime.UtcNow;
            stop.ArrivalLat = request.Lat;
            stop.ArrivalLng = request.Lng;
            route.UpdatedAt = DateTime.UtcNow;
            route.UpdatedBy = actorId;
            await _db.SaveChangesAsync();
        }
        return await _routes.GetRouteAsync(affiliateId, routeId);
    }

    public async Task<RouteDto?> DepartAsync(Guid affiliateId, Guid routeId, Guid stopId, string? actorId, Guid? restrictDriverId = null)
    {
        var route = await LoadAsync(affiliateId, routeId, restrictDriverId);
        var stop = route?.Stops.FirstOrDefault(s => s.Id == stopId);
        if (route is null || stop is null) return null;
        if (route.Status is not ("in_progress" or "completed")) throw new InvalidOperationException("La ruta no está en curso.");

        if (stop.DepartedAt is null)
        {
            stop.DepartedAt = DateTime.UtcNow;
            route.UpdatedAt = DateTime.UtcNow;
            route.UpdatedBy = actorId;
            await _db.SaveChangesAsync();
        }
        return await _routes.GetRouteAsync(affiliateId, routeId);
    }

    public async Task<bool> PostPositionAsync(Guid affiliateId, Guid routeId, PositionRequest request, Guid? restrictDriverId = null)
    {
        ValidateCoords(request.Lat, request.Lng);
        var route = await LoadAsync(affiliateId, routeId, restrictDriverId, includeStops: false);
        if (route is null) return false;
        if (route.Status != "in_progress") throw new InvalidOperationException("La ruta no está en curso.");

        var pos = await _db.VehiclePositions.FirstOrDefaultAsync(p => p.RouteId == routeId && p.AffiliateId == affiliateId);
        if (pos is null)
        {
            pos = new VehiclePosition { AffiliateId = affiliateId, RouteId = routeId };
            _db.VehiclePositions.Add(pos);
        }
        pos.Lat = Math.Round(request.Lat, 6);
        pos.Lng = Math.Round(request.Lng, 6);
        pos.AccuracyMeters = request.AccuracyMeters;
        pos.SpeedKmh = request.SpeedKmh;
        pos.RecordedAt = DateTime.UtcNow;
        pos.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<LiveRouteDto>> GetLiveAsync(Guid affiliateId)
    {
        var routes = await _db.DeliveryRoutes.AsNoTracking().Include(r => r.Vehicle).Include(r => r.Stops)
            .Where(r => r.AffiliateId == affiliateId && r.Status == "in_progress")
            .OrderBy(r => r.DepartedAt).ToListAsync();
        if (routes.Count == 0) return Array.Empty<LiveRouteDto>();

        var routeIds = routes.Select(r => r.Id).ToList();
        var positions = await _db.VehiclePositions.AsNoTracking()
            .Where(p => p.AffiliateId == affiliateId && routeIds.Contains(p.RouteId)).ToDictionaryAsync(p => p.RouteId);

        var driverIds = routes.Where(r => r.DriverId.HasValue).Select(r => r.DriverId!.Value).Distinct().ToList();
        var driverNames = await _db.TeamMembers.AsNoTracking()
            .Where(t => t.AffiliateId == affiliateId && driverIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name);

        var nextStops = routes.ToDictionary(r => r.Id, r => r.Stops.Where(s => s.Status == "pending").OrderBy(s => s.SortOrder).FirstOrDefault());
        var customerIds = nextStops.Values.Where(s => s is not null).Select(s => s!.CustomerId).Distinct().ToList();
        var customerNames = await _db.Customers.AsNoTracking()
            .Where(c => c.AffiliateId == affiliateId && customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);

        var now = DateTime.UtcNow;
        return routes.Select(r =>
        {
            var next = nextStops[r.Id];
            positions.TryGetValue(r.Id, out var pos);
            return new LiveRouteDto(
                r.Id, r.Name, r.Vehicle?.Plate,
                r.DriverId.HasValue && driverNames.TryGetValue(r.DriverId.Value, out var dn) ? dn : null,
                r.Stops.Count, r.Stops.Count(s => s.Status != "pending"),
                next?.Id, next is not null && customerNames.TryGetValue(next.CustomerId, out var cn) ? cn : null, next?.ArrivedAt,
                pos?.Lat, pos?.Lng, pos?.RecordedAt,
                pos is null ? null : (int)Math.Max(0, (now - pos.RecordedAt).TotalSeconds));
        }).ToList();
    }

    private Task<DeliveryRoute?> LoadAsync(Guid affiliateId, Guid routeId, Guid? restrictDriverId, bool includeStops = true)
    {
        var q = _db.DeliveryRoutes.AsQueryable();
        if (includeStops) q = q.Include(r => r.Stops);
        if (restrictDriverId.HasValue) q = q.Where(r => r.DriverId == restrictDriverId.Value);
        return q.FirstOrDefaultAsync(r => r.Id == routeId && r.AffiliateId == affiliateId);
    }

    private static void ValidateCoords(decimal? lat, decimal? lng)
    {
        if (lat.HasValue != lng.HasValue) throw new ArgumentException("Envía latitud y longitud juntas.");
        if (lat is < -90 or > 90) throw new ArgumentException("Latitud no válida.");
        if (lng is < -180 or > 180) throw new ArgumentException("Longitud no válida.");
    }
}
