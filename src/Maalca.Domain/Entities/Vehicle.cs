using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>Vehículo de reparto (camión/camioneta) con su conductor habitual.</summary>
public class Vehicle : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Plate { get; set; } = string.Empty;
    /// <summary>Nombre corto para reconocerlo, ej. "Camión 1".</summary>
    public string? Name { get; set; }
    /// <summary>Cuántos envases (botellones) caben. 0 = sin dato.</summary>
    public int CapacityContainers { get; set; } = 0;
    /// <summary>Conductor habitual (<see cref="TeamMember"/>). Sin FK: borrar al miembro no debe tumbar el vehículo; el servicio lo valida.</summary>
    public Guid? DriverId { get; set; }
    /// <summary>"disponible" | "preparacion" | "en_ruta" | "entrega" | "mantenimiento".</summary>
    public string Status { get; set; } = "disponible";
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public Affiliate? Affiliate { get; set; }
}
