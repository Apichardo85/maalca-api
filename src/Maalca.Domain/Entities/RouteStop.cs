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

    public DeliveryRoute? Route { get; set; }
    public Customer? Customer { get; set; }
}
