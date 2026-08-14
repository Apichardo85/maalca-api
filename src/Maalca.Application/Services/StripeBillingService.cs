using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace Maalca.Application.Services;

public class StripeBillingService : IStripeBillingService
{
    private readonly AppDbContext _db;

    public StripeBillingService(AppDbContext db) => _db = db;

    public async Task<CheckoutSessionResponseDto> CreateCheckoutSessionAsync(Guid affiliateId, CreateCheckoutSessionRequest request)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId)
            ?? throw new KeyNotFoundException("Affiliate not found");

        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        var priceId = Environment.GetEnvironmentVariable("STRIPE_PRICE_ENTREPRENEUR") ?? "";

        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            LineItems = new List<SessionLineItemOptions>
            {
                new() { Price = priceId, Quantity = 1 }
            },
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            ClientReferenceId = affiliateId.ToString()
        };

        if (!string.IsNullOrEmpty(affiliate.StripeCustomerId))
            options.Customer = affiliate.StripeCustomerId;
        else if (!string.IsNullOrEmpty(affiliate.ContactEmail))
            options.CustomerEmail = affiliate.ContactEmail;

        var session = await new SessionService().CreateAsync(options);

        if (string.IsNullOrEmpty(affiliate.StripeCustomerId) && !string.IsNullOrEmpty(session.CustomerId))
        {
            affiliate.StripeCustomerId = session.CustomerId;
            await _db.SaveChangesAsync();
        }

        return new CheckoutSessionResponseDto(session.Url);
    }

    public async Task<PortalSessionResponseDto> CreatePortalSessionAsync(Guid affiliateId, string returnUrl)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId)
            ?? throw new KeyNotFoundException("Affiliate not found");

        if (string.IsNullOrEmpty(affiliate.StripeCustomerId))
            throw new KeyNotFoundException("Este negocio todavía no tiene una suscripción con Stripe.");

        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";

        var session = await new Stripe.BillingPortal.SessionService().CreateAsync(
            new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = affiliate.StripeCustomerId,
                ReturnUrl = returnUrl,
            });

        return new PortalSessionResponseDto(session.Url);
    }

    public async Task HandleWebhookEventAsync(string json, string signatureHeader)
    {
        var webhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? "";
        var stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, webhookSecret);

        if (await _db.StripeProcessedEvents.AnyAsync(e => e.EventId == stripeEvent.Id))
            return;

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await HandleCheckoutSessionCompletedAsync(stripeEvent);
                break;

            case "customer.subscription.updated":
                await HandleSubscriptionUpdatedAsync(stripeEvent);
                break;

            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync(stripeEvent);
                break;
        }

        _db.StripeProcessedEvents.Add(new StripeProcessedEvent
        {
            EventId = stripeEvent.Id,
            ProcessedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private async Task HandleCheckoutSessionCompletedAsync(Event stripeEvent)
    {
        if (stripeEvent.Data.Object is not Session session) return;
        if (!Guid.TryParse(session.ClientReferenceId, out var affiliateId)) return;

        var affiliate = await _db.Affiliates.FindAsync(affiliateId);
        if (affiliate is null) return;

        affiliate.Plan = Maalca.Domain.Enums.Plan.Entrepreneur;
        affiliate.PlanStatus = PlanStatus.Active;
        affiliate.PlanStartedAt ??= DateTime.UtcNow;
        if (!string.IsNullOrEmpty(session.SubscriptionId))
            affiliate.StripeSubscriptionId = session.SubscriptionId;
        if (!string.IsNullOrEmpty(session.CustomerId))
            affiliate.StripeCustomerId = session.CustomerId;
    }

    private async Task HandleSubscriptionUpdatedAsync(Event stripeEvent)
    {
        if (stripeEvent.Data.Object is not Subscription subscription) return;

        var affiliate = await _db.Affiliates.FirstOrDefaultAsync(a => a.StripeCustomerId == subscription.CustomerId);
        if (affiliate is null) return;

        affiliate.StripeSubscriptionId = subscription.Id;
        affiliate.PlanStatus = subscription.Status switch
        {
            "active" or "trialing" => PlanStatus.Active,
            "past_due" => PlanStatus.PastDue,
            "canceled" or "unpaid" => PlanStatus.Canceled,
            _ => affiliate.PlanStatus
        };
    }

    private async Task HandleSubscriptionDeletedAsync(Event stripeEvent)
    {
        if (stripeEvent.Data.Object is not Subscription subscription) return;

        var affiliate = await _db.Affiliates.FirstOrDefaultAsync(a => a.StripeCustomerId == subscription.CustomerId);
        if (affiliate is null) return;

        affiliate.Plan = Maalca.Domain.Enums.Plan.Free;
        affiliate.PlanStatus = PlanStatus.Canceled;
    }
}
