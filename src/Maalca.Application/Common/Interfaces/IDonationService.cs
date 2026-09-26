using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IDonationService
{
    /// <summary>
    /// Crea la donación (siempre, Pending) y, si el afiliado tiene Stripe Connect con cargos
    /// habilitados, además una Checkout Session de cobro directo contra su cuenta conectada.
    /// Resuelve por slug (llamado público, sin auth) — devuelve null si el afiliado no existe,
    /// no está publicado, o no es businessType Community.
    /// </summary>
    Task<CreateDonationResponseDto?> CreateDonationAsync(string affiliateSlug, CreateDonationRequest request);

    /// <summary>
    /// Confirmación desde el webhook de Stripe Connect (checkout.session.completed) — mismo
    /// patrón que OrderService.ConfirmFromWebhookAsync: no-op silencioso si el Session no le
    /// pertenece a ninguna Donation (busca por StripeCheckoutSessionId).
    /// </summary>
    Task ConfirmFromWebhookAsync(string checkoutSessionId, string? paymentIntentId);

    /// <summary>
    /// Total recaudado real (Status=Paid) en lo que va del mes calendario, zona horaria del
    /// afiliado. Null si el afiliado no tiene Stripe Connect activo (nunca puede haber
    /// donaciones Paid en ese caso) — el frontend cae al monto reportado a mano.
    /// </summary>
    Task<PublicDonationSummaryDto?> GetPublicSummaryAsync(string affiliateSlug);
}
