using Maalca.Application.Common.Payments;
using Maalca.Domain.Entities;
using Stripe;
using Stripe.Checkout;

namespace Maalca.Application.Services.Payments;

/// <summary>
/// Stripe Connect: Checkout Session en modo "payment" ejecutada CON el header Stripe-Account de la cuenta
/// conectada del negocio (direct charge) — el dinero entra directo a su cuenta, MaalCa nunca lo toca.
/// Mismo comportamiento que tenía OrderService antes de la interfaz; solo se movió aquí.
/// </summary>
public class StripePaymentProvider : IPaymentProvider
{
    public string Key => "stripe";

    public bool IsReady(Affiliate affiliate) =>
        affiliate.StripeConnectChargesEnabled && !string.IsNullOrEmpty(affiliate.StripeConnectAccountId);

    public async Task<PaymentCheckoutResult> CreateCheckoutAsync(Affiliate affiliate, PaymentCheckoutRequest request)
    {
        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        var requestOptions = new RequestOptions { StripeAccount = affiliate.StripeConnectAccountId };
        var currency = request.Currency.ToLowerInvariant();

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            LineItems = request.Lines.Select(l => new SessionLineItemOptions
            {
                Quantity = l.Quantity,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = currency,
                    UnitAmount = (long)Math.Round(l.UnitAmount * 100),
                    ProductData = new SessionLineItemPriceDataProductDataOptions { Name = l.Name },
                },
            }).ToList(),
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            ClientReferenceId = request.OrderId.ToString(),
            CustomerEmail = string.IsNullOrEmpty(request.CustomerEmail) ? null : request.CustomerEmail,
        };
        if (request.ExpiresAt is { } expiresAt) options.ExpiresAt = expiresAt;
        // Pide el teléfono en el Checkout: junto con el nombre/correo que Stripe ya captura, completa el
        // cliente cuando no lo dio antes de pagar (ver OrderService.MarkPaidAsync).
        if (request.CollectPhone) options.PhoneNumberCollection = new SessionPhoneNumberCollectionOptions { Enabled = true };

        var session = await new SessionService().CreateAsync(options, requestOptions);
        return new PaymentCheckoutResult(session.Id, session.Url);
    }

    public async Task<PaymentCheckoutStatus> GetCheckoutStatusAsync(Affiliate affiliate, string checkoutId)
    {
        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        var requestOptions = new RequestOptions { StripeAccount = affiliate.StripeConnectAccountId };
        var session = await new SessionService().GetAsync(checkoutId, requestOptions: requestOptions);
        return new PaymentCheckoutStatus(
            session.PaymentStatus == "paid", session.PaymentIntentId,
            session.CustomerDetails?.Name, session.CustomerDetails?.Email, session.CustomerDetails?.Phone);
    }
}

/// <summary>Hoy todo negocio cobra con Stripe. Con una segunda pasarela, aquí se elige según la configuración del negocio.</summary>
public class PaymentProviderResolver : IPaymentProviderResolver
{
    private readonly IReadOnlyDictionary<string, IPaymentProvider> _providers;

    public PaymentProviderResolver(IEnumerable<IPaymentProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IPaymentProvider For(Affiliate affiliate) => _providers["stripe"];
}
