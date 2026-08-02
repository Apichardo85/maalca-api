using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IStripeBillingService
{
    Task<CheckoutSessionResponseDto> CreateCheckoutSessionAsync(Guid affiliateId, CreateCheckoutSessionRequest request);
    Task HandleWebhookEventAsync(string json, string signatureHeader);
}
