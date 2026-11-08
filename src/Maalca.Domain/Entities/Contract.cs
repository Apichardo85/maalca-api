using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Contrato recurrente de un cliente (ej. agua y hielo por contrato): cada cuánto se le entrega,
/// cada cuánto se le cobra, cuántos días de crédito tiene y los precios acordados. Un cliente puede
/// tener varios a lo largo del tiempo, pero solo uno activo a la vez (lo valida el servicio, no la BD).
/// </summary>
public class Contract : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public Guid CustomerId { get; set; }
    /// <summary>"daily" | "weekly" | "on_demand".</summary>
    public string DeliveryFrequency { get; set; } = "weekly";
    /// <summary>"weekly" | "biweekly" | "monthly".</summary>
    public string BillingCycle { get; set; } = "monthly";
    /// <summary>Días de crédito (ej. 15 o 30). 0 = contado.</summary>
    public int CreditDays { get; set; } = 0;
    /// <summary>Precios acordados por producto, JSON [{"itemId":"…","price":50}, …]. Null = precios de lista.</summary>
    public string? AgreedPrices { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    /// <summary>Folio del contrato (el que lleva el papel, ej. "0133"). Único por negocio. Se puede escribir a mano para
    /// continuar la numeración del cuaderno; si se omite al crear, el servidor asigna el siguiente.</summary>
    public string? ContractNumber { get; set; }
    /// <summary>Anaqueles prestados al cliente según el contrato.</summary>
    public int RacksLoaned { get; set; } = 0;
    /// <summary>Botellones prestados al cliente según el contrato (cupo: sirve para avisar si tiene de más).</summary>
    public int BottlesLoaned { get; set; } = 0;

    // ── Aceptación en línea (portal del cliente) ──────────────────────────────────────────────
    // El cliente lee el contrato en su cuenta y lo acepta escribiendo su nombre. Se guarda quién, cuándo y QUÉ texto vio
    // (huella SHA-256 del documento ya lleno): si el contrato cambia después, la huella ya no coincide y debe aceptarlo de nuevo.
    public DateTime? AcceptedAt { get; set; }
    public string? AcceptedByName { get; set; }
    public string? AcceptedByEmail { get; set; }
    public Guid? AcceptedByStoreCustomerId { get; set; }
    public string? AcceptedDocumentHash { get; set; }

    public Affiliate? Affiliate { get; set; }
    public Customer? Customer { get; set; }
}
