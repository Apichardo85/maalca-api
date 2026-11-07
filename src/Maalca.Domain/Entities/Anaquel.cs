using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Anaquel (rack) instalado en el local de un cliente, con un código QR único. El conductor lo escanea
/// para identificar cliente y ubicación y registrar llenos entregados / vacíos recogidos.
/// </summary>
public class Anaquel : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public Guid? CustomerId { get; set; }
    /// <summary>Código único del QR (el QR codifica este valor). Único en toda la plataforma para poder resolverlo sin saber el negocio.</summary>
    public string QrCode { get; set; } = string.Empty;
    /// <summary>Referencia legible, ej. "A-014".</summary>
    public string? Label { get; set; }
    /// <summary>Ubicación dentro del local del cliente, ej. "Cocina, junto a la nevera".</summary>
    public string? Location { get; set; }
    /// <summary>Cuántos envases caben.</summary>
    public int Capacity { get; set; } = 0;
    /// <summary>"disponible" | "activo" | "pendiente_reposicion" | "completo" | "danado" | "fuera_servicio".</summary>
    public string Status { get; set; } = "disponible";
    public string? Notes { get; set; }

    public Affiliate? Affiliate { get; set; }
    public Customer? Customer { get; set; }
}
