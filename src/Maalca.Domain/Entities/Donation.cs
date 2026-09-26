using Maalca.Domain.Common;
using Maalca.Domain.Enums;

namespace Maalca.Domain.Entities;

/// <summary>
/// Donación monetaria real de un visitante a un afiliado Community. Antes de esto, "Recaudado
/// este mes" en Affiliate.CommunityImpact era un número que el afiliado reportaba a mano — no
/// había ningún cobro real detrás del botón "Donar ahora" salvo el fallback de WhatsApp. Mismo
/// patrón que Order (ver OrderService): cobro directo contra la cuenta Stripe Connect del
/// afiliado (RequestOptions.StripeAccount), MaalCa nunca toca el dinero. Se mantiene
/// deliberadamente separada de Order (no es "un pedido más") porque una donación no tiene
/// items/catálogo — es un solo monto — y porque el afiliado sin Stripe Connect activo sigue
/// pudiendo recibir donaciones por WhatsApp exactamente como antes (DonationService no crea
/// fila alguna en ese caso, el frontend cae directo al link de WhatsApp).
/// </summary>
public class Donation : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public Affiliate? Affiliate { get; set; }

    public string? DonorName { get; set; }
    public string? DonorEmail { get; set; }
    /// Mensaje opcional del donante — no se muestra públicamente, solo visible al afiliado.
    public string? Message { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";

    public DonationStatus Status { get; set; } = DonationStatus.Pending;

    public string? StripeCheckoutSessionId { get; set; }
    public string? StripePaymentIntentId { get; set; }
}
