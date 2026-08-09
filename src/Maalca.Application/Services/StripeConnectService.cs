using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace Maalca.Application.Services;

/// <summary>
/// Cuenta conectada vía Accounts v2 (configuración "merchant", dashboard "full" — el
/// equivalente v2 de lo que era Standard en v1: menor responsabilidad para MaalCa, el
/// afiliado es el merchant of record de sus propias ventas). Se migró de v1 a v2 el
/// 2026-08-09 porque esta cuenta de Stripe ya no permite crear cuentas conectadas con la API
/// v1 ("Stripe no longer recommends Accounts v1 for new Connect integrations").
///
/// El resto del flujo NO cambió: Account Links (onboarding hospedado) y AccountService.GetAsync
/// (status) siguen siendo v1 — Stripe permite pasar el id de una cuenta v2 a esos endpoints v1
/// sin problema (la respuesta viene con forma v1). Solo la CREACIÓN pasó a v2. Ver
/// docs.stripe.com/connect/accounts-v2/migrate-integration.
///
/// MaalCa nunca toca el dinero del afiliado: no hay transferencias ni application fee en esta
/// primera versión.
/// </summary>
public class StripeConnectService : IStripeConnectService
{
    private readonly AppDbContext _db;

    public StripeConnectService(AppDbContext db) => _db = db;

    public async Task<ConnectOnboardingLinkResponseDto> CreateOnboardingLinkAsync(Guid affiliateId, CreateConnectOnboardingLinkRequest request)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId)
            ?? throw new KeyNotFoundException("Affiliate not found");

        var apiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        StripeConfiguration.ApiKey = apiKey;

        if (string.IsNullOrEmpty(affiliate.StripeConnectAccountId))
        {
            // Country viene de Affiliate.Country (ISO alpha-2, configurado por el afiliado en
            // Configuración). "US" es solo el último recurso si nunca lo configuró — Stripe no
            // permite cambiar el país de una cuenta conectada después de creada, así que este
            // fallback puede dejar mal configurado a un afiliado que no sea de EE.UU. y nunca
            // tocó ese campo. El fix real es forzar a completarlo en el onboarding, no aquí.
            var client = new StripeClient(apiKey);
            var v2Options = new Stripe.V2.Core.AccountCreateOptions
            {
                ContactEmail = string.IsNullOrEmpty(affiliate.ContactEmail) ? null : affiliate.ContactEmail,
                Dashboard = "full",
                Identity = new Stripe.V2.Core.AccountCreateIdentityOptions
                {
                    Country = string.IsNullOrEmpty(affiliate.Country) ? "US" : affiliate.Country,
                },
                Configuration = new Stripe.V2.Core.AccountCreateConfigurationOptions
                {
                    Merchant = new Stripe.V2.Core.AccountCreateConfigurationMerchantOptions
                    {
                        Capabilities = new Stripe.V2.Core.AccountCreateConfigurationMerchantCapabilitiesOptions
                        {
                            CardPayments = new Stripe.V2.Core.AccountCreateConfigurationMerchantCapabilitiesCardPaymentsOptions { Requested = true },
                        },
                    },
                },
            };
            var account = await client.V2.Core.Accounts.CreateAsync(v2Options);
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
            return new ConnectAccountStatusDto(Connected: false, ChargesEnabled: false, PayoutsEnabled: false, DetailsSubmitted: false, Country: affiliate.Country);

        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        var account = await new AccountService().GetAsync(affiliate.StripeConnectAccountId);

        ApplyAccountStatus(affiliate, account);
        await _db.SaveChangesAsync();

        return new ConnectAccountStatusDto(
            Connected: true,
            ChargesEnabled: affiliate.StripeConnectChargesEnabled,
            PayoutsEnabled: affiliate.StripeConnectPayoutsEnabled,
            DetailsSubmitted: affiliate.StripeConnectDetailsSubmitted,
            Country: affiliate.Country
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
