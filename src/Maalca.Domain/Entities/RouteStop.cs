using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>Una parada de la ruta: un cliente a visitar, en cierto orden.</summary>
public class RouteStop : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public Guid RouteId { get; set; }
    public Guid CustomerId { get; set; }
    public int SortOrder { get; set; }
    /// <summary>"pending" | "visited" | "skipped" | "failed".</summary>
    public string Status { get; set; } = "pending";
    public DateTime? VisitedAt { get; set; }
    /// <summary>Motivo si se saltó o falló (cerrado, no estaba, etc.).</summary>
    public string? Notes { get; set; }
    /// <summary>Modo camión: cuándo el conductor marcó "llegué" y dónde estaba (para validar la visita).</summary>
    public DateTime? ArrivedAt { get; set; }
    public decimal? ArrivalLat { get; set; }
    public decimal? ArrivalLng { get; set; }
    /// <summary>Cuándo el conductor marcó "salí" de la parada.</summary>
    public DateTime? DepartedAt { get; set; }

    /// <summary>
    /// Qué se hace en la parada: "delivery" (se entrega, el reparto de siempre) o "pickup" (se recoge).
    /// Las rutas existentes quedan en "delivery"; nada cambia para ellas.
    /// </summary>
    public string Kind { get; set; } = RouteStopKinds.Delivery;
    /// <summary>
    /// A qué pertenece la parada cuando no es solo "visitar a un cliente": el tipo ("pickup_request",
    /// "shipment"...) y su Id. Así la ruta no queda atada a un tipo de negocio. Null en el reparto normal.
    /// </summary>
    public string? SubjectType { get; set; }
    public Guid? SubjectId { get; set; }

    public DeliveryRoute? Route { get; set; }
    public Customer? Customer { get; set; }
    /// <summary>Lo que se espera encontrar en la parada, para comparar contra lo que realmente se registró.</summary>
    public ICollection<RouteStopLine> PlannedLines { get; set; } = new List<RouteStopLine>();
}

public static class RouteStopKinds
{
    public const string Delivery = "delivery";
    public const string Pickup = "pickup";
}
