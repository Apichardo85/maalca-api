using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IStripeBillingService
{
    Task<CheckoutSessionResponseDto> CreateCheckoutSessionAsync(Guid affiliateId, CreateCheckoutSessionRequest request);

    /// <summary>
    /// Sesión del Stripe Billing Portal — self-service para que el dueño actualice su método
    /// de pago o vea sus facturas. Requiere que el afiliado ya tenga StripeCustomerId (es decir,
    /// haya pasado por checkout al menos una vez); si no, lanza KeyNotFoundException.
    /// </summary>
    Task<PortalSessionResponseDto> CreatePortalSessionAsync(Guid affiliateId, string returnUrl);

    Task HandleWebhookEventAsync(string json, string signatureHeader);
}
