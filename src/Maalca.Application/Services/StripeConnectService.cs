using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace Maalca.Application.Services;

/// <summary>
/// Cuenta conectada Standard + direct charges. El afiliado es el merchant of record de sus
/// propias ventas (Stripe lo trata como "SaaS platform", no "marketplace" — ver
/// docs.stripe.com/connect/saas-platforms-and-marketplaces). MaalCa nunca toca el dinero del
/// afiliado: no hay transferencias ni application fee en esta primera versión.
/// </summary>
public class StripeConnectService : IStripeConnectService
{
    private readonly AppDbContext _db;

    public StripeConnectService(AppDbContext db) => _db = db;

    public async Task<ConnectOnboardingLinkResponseDto> CreateOnboardingLinkAsync(Guid affiliateId, CreateConnectOnboardingLinkRequest request)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId)
            ?? throw new KeyNotFoundException("Affiliate not found");

        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";

        if (string.IsNullOrEmpty(affiliate.StripeConnectAccountId))
        {
            // NOTA: Country hardcodeado a "US" — el modelo Affiliate no tiene un campo de país
            // hoy. Si en el futuro se agrega, leerlo de ahí en vez de asumir. Ver
            // plans/spec-maalca-api-espacio-v2.md.
            var accountOptions = new AccountCreateOptions
            {
                Type = "standard",
                Country = "US",
                Email = string.IsNullOrEmpty(affiliate.ContactEmail) ? null : affiliate.ContactEmail,
            };
            var account = await new AccountService().CreateAsync(accountOptions);
            affiliate.StripeConnectAccountId = account.Id;
            await _db.SaveChangesAsync();
        }

        var linkOptions = new AccountLinkCreateOptions
        {
            Account = affiliate.StripeConnectAccountId,
            Type = "account_onboarding",
            ReturnUrl = request.ReturnUrl,
            RefreshUrl = request.RefreshUrl,
        };
        var link = await new AccountLinkService().CreateAsync(linkOptions);

        return new ConnectOnboardingLinkResponseDto(link.Url);
    }

    public async Task<ConnectAccountStatusDto> GetStatusAsync(Guid affiliateId)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId)
            ?? throw new KeyNotFoundException("Affiliate not found");

        if (string.IsNullOrEmpty(affiliate.StripeConnectAccountId))
            return new ConnectAccountStatusDto(Connected: false, ChargesEnabled: false, PayoutsEnabled: false, DetailsSubmitted: false);

        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        var account = await new AccountService().GetAsync(affiliate.StripeConnectAccountId);

        ApplyAccountStatus(affiliate, account);
        await _db.SaveChangesAsync();

        return new ConnectAccountStatusDto(
            Connected: true,
            ChargesEnabled: affiliate.StripeConnectChargesEnabled,
            PayoutsEnabled: affiliate.StripeConnectPayoutsEnabled,
            DetailsSubmitted: affiliate.StripeConnectDetailsSubmitted
        );
    }

    public async Task HandleWebhookEventAsync(string json, string signatureHeader)
    {
        // Webhook separado del de facturación (distinto secret) — Connect envía eventos de
        // account.* a este endpoint, configurado aparte en el Dashboard de Stripe.
        var webhookSecret = Environment.GetEnvironmentVariable("STRIPE_CONNECT_WEBHOOK_SECRET") ?? "";
        var stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, webhookSecret);

        if (await _db.StripeProcessedEvents.AnyAsync(e => e.EventId == stripeEvent.Id))
            return;

        if (stripeEvent.Type == "account.updated" && stripeEvent.Data.Object is Account account)
        {
            var affiliate = await _db.Affiliates.FirstOrDefaultAsync(a => a.StripeConnectAccountId == account.Id);
            if (affiliate is not null)
                ApplyAccountStatus(affiliate, account);
        }

        _db.StripeProcessedEvents.Add(new StripeProcessedEvent
        {
            EventId = stripeEvent.Id,
            ProcessedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private static void ApplyAccountStatus(Domain.Entities.Affiliate affiliate, Account account)
    {
        affiliate.StripeConnectChargesEnabled = account.ChargesEnabled;
        affiliate.StripeConnectPayoutsEnabled = account.PayoutsEnabled;
        affiliate.StripeConnectDetailsSubmitted = account.DetailsSubmitted;
        affiliate.StripeConnectUpdatedAt = DateTime.UtcNow;
    }
}
