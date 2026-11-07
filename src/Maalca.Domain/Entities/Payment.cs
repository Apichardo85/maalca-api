using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Pago o abono de un cliente. Es el libro de cobros: el saldo vivo de un cliente es la suma de sus
/// facturas menos la suma de sus pagos. Un pago puede ir ligado a una factura (InvoiceId) o quedar
/// como abono a cuenta (null). Nunca se edita ni se borra: para corregir se registra un movimiento nuevo.
/// </summary>
public class Payment : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? InvoiceId { get; set; }
    public decimal Amount { get; set; }
    /// <summary>"cash" | "transfer".</summary>
    public string Method { get; set; } = "cash";
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    /// <summary>Quién lo registró (usuario o conductor).</summary>
    public string? RegisteredBy { get; set; }
    /// <summary>Clave que genera el cliente (celular en ruta) para que un reintento con mala señal no duplique el cobro.
    /// Única por afiliado cuando no es null.</summary>
    public string? IdempotencyKey { get; set; }

    public Affiliate? Affiliate { get; set; }
    public Customer? Customer { get; set; }
    public Invoice? Invoice { get; set; }
}
