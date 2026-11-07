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

    public Affiliate? Affiliate { get; set; }
    public Customer? Customer { get; set; }
}
