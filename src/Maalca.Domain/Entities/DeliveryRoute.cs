using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Ruta de reparto de un día: qué vehículo y conductor la hacen y a qué clientes visitan, en orden.
/// (Se llama DeliveryRoute para no chocar con Route de ASP.NET.)
/// </summary>
public class DeliveryRoute : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly RouteDate { get; set; }
    public Guid? VehicleId { get; set; }
    /// <summary>Conductor de esta ruta (<see cref="TeamMember"/>); por defecto el del vehículo. Sin FK, igual que en Vehicle.</summary>
    public Guid? DriverId { get; set; }
    /// <summary>"planned" | "loading" | "in_progress" | "completed" | "cancelled".</summary>
    public string Status { get; set; } = "planned";
    /// <summary>Envases llenos cargados al salir de planta (para conciliar al cerrar).</summary>
    public int? LoadedFull { get; set; }
    public DateTime? DepartedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }
    /// <summary>Ruta fija de la que salió (null si se armó a mano). Única por (plantilla, fecha): evita duplicar el día.</summary>
    public Guid? TemplateId { get; set; }

    public Affiliate? Affiliate { get; set; }
    public Vehicle? Vehicle { get; set; }
    public ICollection<RouteStop> Stops { get; set; } = new List<RouteStop>();
}
