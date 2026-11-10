using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Ruta fija: se repite sola los días marcados. Cada día se genera una <see cref="DeliveryRoute"/> a partir de ella
/// (camión, conductor y paradas en orden). Cambiar el conductor de la ruta de un día no toca la plantilla.
/// </summary>
public class RouteTemplate : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? VehicleId { get; set; }
    /// <summary>Conductor habitual (<see cref="TeamMember"/>); si falta se usa el del camión. Sin FK, igual que en Vehicle.</summary>
    public Guid? DriverId { get; set; }
    /// <summary>Días en que corre, como bits: bit N = DayOfWeek N (domingo = 0 … sábado = 6).</summary>
    public int DaysMask { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public Affiliate? Affiliate { get; set; }
    public Vehicle? Vehicle { get; set; }
    public ICollection<RouteTemplateStop> Stops { get; set; } = new List<RouteTemplateStop>();
}

/// <summary>Un cliente de la ruta fija, en su orden de visita.</summary>
public class RouteTemplateStop : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public Guid TemplateId { get; set; }
    public Guid CustomerId { get; set; }
    public int SortOrder { get; set; }

    public RouteTemplate? Template { get; set; }
    public Customer? Customer { get; set; }
}
