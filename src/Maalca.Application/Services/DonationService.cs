using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace Maalca.Application.Services;

/// <summary>
/// Donaciones reales del storefront público de Community. Mismo patrón que OrderService:
/// cobro (cuando el afiliado tiene Connect activo) vía Checkout Session en modo "payment",
/// ejecutada CON el header Stripe-Account de la cuenta conectada del afiliado — direct charge,
/// el dinero entra directo a la cuenta del afiliado, MaalCa nunca la toca. Sin Connect activo,
/// no se crea Checkout Session ninguna (CheckoutUrl null) — el storefront cae al link de
/// WhatsApp existente, exactamente como ya hacía antes de esto.
/// </summary>
public class DonationService : IDonationService
{
    private readonly AppDbContext _db;

    public DonationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CreateDonationResponseDto?> CreateDonationAsync(string affiliateSlug, CreateDonationRequest request)
    {
        var affiliate = await _db.Affiliates
            .FirstOrDefaultAsync(a => a.Slug == affiliateSlug && a.Published && a.BusinessType == BusinessType.Community);
        if (affiliate is null) return null;
        if (request.Amount <= 0) throw new ArgumentException("El monto de la donación debe ser mayor a 0.");

        var donation = new Donation
        {
            AffiliateId = affiliate.Id,
            DonorName = request.DonorName,
            DonorEmail = request.DonorEmail,
            Message = request.Message,
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.ToUpperInvariant(),
            Status = DonationStatus.Pending,
        };
        _db.Donations.Add(donation);
        await _db.SaveChangesAsync();

        // Sin Connect activo: la donación queda guardada igual (visible al afiliado si más
        // adelante hay un listado), pero no hay cobro online — el storefront cae al WhatsApp de
        // siempre.
        if (!affiliate.StripeConnectChargesEnabled || string.IsNullOrEmpty(affiliate.StripeConnectAccountId))
            return new CreateDonationResponseDto(donation.Id, CheckoutUrl: null);

        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        var requestOptions = new RequestOptions { StripeAccount = affiliate.StripeConnectAccountId };

        var session = await new SessionService().CreateAsync(new SessionCreateOptions
        {
            Mode = "payment",
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = donation.Currency.ToLowerInvariant(),
                        UnitAmount = (long)Math.Round(donation.Amount * 100),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Donación — {affiliate.Name}",
                        },
                    },
                },
            },
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            ClientReferenceId = donation.Id.ToString(),
            CustomerEmail = string.IsNullOrEmpty(request.DonorEmail) ? null : request.DonorEmail,
        }, requestOptions);

        donation.StripeCheckoutSessionId = session.Id;
        await _db.SaveChangesAsync();

        return new CreateDonationResponseDto(donation.Id, session.Url);
    }

    public async Task ConfirmFromWebhookAsync(string checkoutSessionId, string? paymentIntentId)
    {
        var donation = await _db.Donations
            .FirstOrDefaultAsync(d => d.StripeCheckoutSessionId == checkoutSessionId);
        if (donation is null || donation.Status != DonationStatus.Pending) return; // no es nuestro, o ya confirmada

        donation.Status = DonationStatus.Paid;
        donation.StripePaymentIntentId = paymentIntentId;
        donation.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<PublicDonationSummaryDto?> GetPublicSummaryAsync(string affiliateSlug)
    {
        var affiliate = await _db.Affiliates
            .Where(a => a.Slug == affiliateSlug && a.BusinessType == BusinessType.Community)
            .Select(a => new { a.Id, a.Timezone, a.StripeConnectChargesEnabled })
            .FirstOrDefaultAsync();
        if (affiliate is null) return null;
        if (!affiliate.StripeConnectChargesEnabled) return new PublicDonationSummaryDto(RaisedThisMonth: null);

        var (startUtc, endUtc) = CurrentMonthUtc(affiliate.Timezone);
        var raised = await _db.Donations
            .Where(d => d.AffiliateId == affiliate.Id && d.Status == DonationStatus.Paid
                && d.CreatedAt >= startUtc && d.CreatedAt < endUtc)
            .SumAsync(d => (decimal?)d.Amount) ?? 0m;

        return new PublicDonationSummaryDto(Math.Round(raised, 2, MidpointRounding.AwayFromZero));
    }

    // Mismo cálculo que CommunityService.CurrentMonthUtc — duplicado a propósito en vez de
    // compartido, ambos son helpers privados chicos de servicios distintos sin dependencia
    // entre sí (ver comentario equivalente ya existente en el resto del código para este mismo
    // patrón de "mes calendario en la zona horaria del afiliado").
    private static (DateTime StartUtc, DateTime EndUtc) CurrentMonthUtc(string? ianaTimezone)
    {
        var tz = TimeZoneInfo.Utc;
        if (!string.IsNullOrWhiteSpace(ianaTimezone))
        {
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(ianaTimezone); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var localStart = new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var start = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
        var end = TimeZoneInfo.ConvertTimeToUtc(localStart.AddMonths(1), tz);
        return (start, end);
    }
}
