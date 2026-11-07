using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

public class Customer : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Active"; // Active, Inactive
    public DateTime? LastVisit { get; set; }
    public int TotalVisits { get; set; } = 0;
    /// Pedidos para recoger que nunca vino a buscar (marcados "No vino" en la Estación/Pedidos).
    public int NoShows { get; set; } = 0;

    // ── Clientes por contrato (Nueva Era / agua y hielo, Fase 1) ─────────────
    // Todos opcionales: un cliente sin ellos se comporta exactamente como antes.
    /// <summary>RNC o cédula del cliente (para la factura fiscal futura).</summary>
    public string? Rnc { get; set; }
    public string? Address { get; set; }
    /// <summary>Ubicación del negocio para ubicarlo en la ruta de reparto.</summary>
    public decimal? GpsLat { get; set; }
    public decimal? GpsLng { get; set; }
    /// <summary>Ruta habitual de reparto. Sin FK por ahora: la entidad Route llega en la Fase 2.</summary>
    public Guid? RouteId { get; set; }
    /// <summary>Frecuencia de cobro: "weekly" | "biweekly" | "monthly". Null = sin ciclo definido.</summary>
    public string? BillingCycle { get; set; }
    /// <summary>Frecuencia de entrega: "daily" | "weekly" | "on_demand". Null = sin definir.</summary>
    public string? DeliveryFrequency { get; set; }
    /// <summary>Días de crédito (ej. 15 o 30). Null/0 = contado.</summary>
    public int? CreditDays { get; set; }
    /// <summary>Lista de precios que aplica a este cliente (PriceList). Sin navegación en esta tarea.</summary>
    public Guid? PriceListId { get; set; }

    public Affiliate? Affiliate { get; set; }
}
