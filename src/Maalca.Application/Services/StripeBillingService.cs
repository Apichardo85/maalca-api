using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
// Alias: Stripe también define Plan e Invoice; estos nombres apuntan a los nuestros / los de Stripe sin ambigüedad.
using Plan = Maalca.Domain.Enums.Plan;
using StripeInvoice = Stripe.Invoice;

namespace Maalca.Application.Services;

/// <summary>
/// Suscripción MaalCa → afiliado. Los precios se resuelven por lookup_key (maalca_*), no por id:
/// así test y live usan exactamente el mismo código y cambiar de modo es cambiar la llave y los
/// webhook secrets — ver maalca-web/docs/billing/stripe.md (Fase B).
///
/// Nombres públicos ↔ internos: Starter = Plan.Free (30 días gratis), Growth = Entrepreneur,
/// Professional = Enterprise.
///
/// Stripe es la fuente de verdad: el webhook sincroniza plan, intervalo, pantallas extra y estado
/// desde la suscripción (customer.subscription.*), así que un cambio hecho en el Billing Portal
/// o directo en el Dashboard de Stripe también llega a MaalCa.
/// </summary>
public class StripeBillingService : IStripeBillingService
{
    public const string GrowthMonthly = "maalca_growth_monthly";
    public const string GrowthYearly = "maalca_growth_yearly";
    public const string ProfessionalMonthly = "maalca_professional_monthly";
    public const string ProfessionalYearly = "maalca_professional_yearly";
    public const string SetupFee = "maalca_setup_fee";
    public const string ExtraScreenMonthly = "maalca_extra_screen_monthly";
    public const string ExtraScreenYearly = "maalca_extra_screen_yearly";

    public const string AlreadySubscribedMessage = "Este negocio ya tiene una suscripción activa. Cambia el plan desde \"Administrar suscripción\".";
    public const string NoSubscriptionMessage = "Primero activa un plan; las pantallas extra se agregan a esa suscripción.";
    public const int MaxExtraScreens = 20;

    private static readonly string[] AllLookupKeys =
    {
        GrowthMonthly, GrowthYearly, ProfessionalMonthly, ProfessionalYearly,
        SetupFee, ExtraScreenMonthly, ExtraScreenYearly,
    };

    // Cache de precios por proceso (10 min): el resumen del plan se pide en cada carga del
    // dashboard y los precios casi nunca cambian. Se invalida solo si cambia la llave (test→live).
    private static readonly SemaphoreSlim PriceLock = new(1, 1);
    private static Dictionary<string, Price>? _prices;
    private static DateTime _pricesAt;
    private static string? _pricesKey;

    private readonly AppDbContext _db;
    private readonly IPlanLimitService _planLimit;

    public StripeBillingService(AppDbContext db, IPlanLimitService planLimit)
    {
        _db = db;
        _planLimit = planLimit;
    }

    private static string UseApiKey()
    {
        var key = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
        StripeConfiguration.ApiKey = key;
        return key;
    }

    public static string PublicPlanName(Plan plan) => plan switch
    {
        Plan.Entrepreneur => "Growth",
        Plan.Enterprise => "Professional",
        _ => "Starter",
    };

    private static Plan ParsePlan(string? plan) =>
        plan?.Trim().ToLowerInvariant() is "enterprise" or "professional" ? Plan.Enterprise : Plan.Entrepreneur;

    private static string ParseInterval(string? interval) =>
        interval?.Trim().ToLowerInvariant() is "year" or "yearly" or "annual" or "anual" ? "year" : "month";

    private static string PlanKey(Plan plan, string interval) => (plan, interval) switch
    {
        (Plan.Enterprise, "year") => ProfessionalYearly,
        (Plan.Enterprise, _) => ProfessionalMonthly,
        (_, "year") => GrowthYearly,
        _ => GrowthMonthly,
    };

    private static string ExtraScreenKey(string interval) => interval == "year" ? ExtraScreenYearly : ExtraScreenMonthly;

    private static Plan? PlanFromLookupKey(string? key) => key switch
    {
        GrowthMonthly or GrowthYearly => Plan.Entrepreneur,
        ProfessionalMonthly or ProfessionalYearly => Plan.Enterprise,
        _ => null,
    };

    private static bool IsExtraScreenKey(string? key) => key is ExtraScreenMonthly or ExtraScreenYearly;

    private static async Task<IReadOnlyDictionary<string, Price>> GetPricesAsync()
    {
        var apiKey = UseApiKey();
        if (_prices is not null && _pricesKey == apiKey && DateTime.UtcNow - _pricesAt < TimeSpan.FromMinutes(10))
            return _prices;

        await PriceLock.WaitAsync();
        try
        {
            if (_prices is not null && _pricesKey == apiKey && DateTime.UtcNow - _pricesAt < TimeSpan.FromMinutes(10))
                return _prices;

            var list = await new PriceService().ListAsync(new PriceListOptions
            {
                LookupKeys = AllLookupKeys.ToList(),
                Active = true,
                Limit = 20,
            });
            var found = list.Data.Where(p => !string.IsNullOrEmpty(p.LookupKey))
                .GroupBy(p => p.LookupKey).ToDictionary(g => g.Key, g => g.First());
            // Una lista vacía no se cachea: si los precios se crean después, se ven al instante.
            if (found.Count == 0) return found;
            _prices = found;
            _pricesAt = DateTime.UtcNow;
            _pricesKey = apiKey;
            return _prices;
        }
        finally
        {
            PriceLock.Release();
        }
    }

    private static async Task<Price> RequirePriceAsync(string lookupKey)
    {
        var prices = await GetPricesAsync();
        return prices.TryGetValue(lookupKey, out var price)
            ? price
            : throw new KeyNotFoundException($"El cobro de este plan todavía no está configurado en la plataforma (falta el precio \"{lookupKey}\" en Stripe).");
    }

    private static bool HasLiveSubscription(Affiliate a) =>
        !string.IsNullOrEmpty(a.StripeSubscriptionId) && a.Plan != Plan.Free
        && a.PlanStatus is PlanStatus.Active or PlanStatus.PastDue;

    // ─────────────────────────────── Checkout ───────────────────────────────

    public async Task<CheckoutSessionResponseDto> CreateCheckoutSessionAsync(Guid affiliateId, CreateCheckoutSessionRequest request)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId)
            ?? throw new KeyNotFoundException("Affiliate not found");

        // Un segundo Checkout crearía una SEGUNDA suscripción (doble cobro). Cambiar de plan,
        // de intervalo o cancelar va por el Billing Portal.
        if (HasLiveSubscription(affiliate))
            throw new InvalidOperationException(AlreadySubscribedMessage);

        var plan = ParsePlan(request.Plan);
        var interval = ParseInterval(request.Interval);
        var extras = Math.Clamp(request.ExtraScreens, 0, MaxExtraScreens);
        var chargeSetup = affiliate.SetupFeePaidAt is null;

        var lineItems = new List<SessionLineItemOptions>
        {
            new() { Price = (await RequirePriceAsync(PlanKey(plan, interval))).Id, Quantity = 1 },
        };
        if (extras > 0)
            lineItems.Add(new() { Price = (await RequirePriceAsync(ExtraScreenKey(interval))).Id, Quantity = extras });
        // Precio one_time dentro de un Checkout de suscripción: Stripe lo agrega solo a la
        // primera factura.
        if (chargeSetup)
            lineItems.Add(new() { Price = (await RequirePriceAsync(SetupFee)).Id, Quantity = 1 });

        var metadata = new Dictionary<string, string>
        {
            ["plan"] = plan.ToString(),
            ["interval"] = interval,
            ["affiliateId"] = affiliateId.ToString(),
            ["setup"] = chargeSetup ? "1" : "0",
        };

        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            LineItems = lineItems,
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            ClientReferenceId = affiliateId.ToString(),
            AllowPromotionCodes = true,
            Metadata = metadata,
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                // affiliateId en la suscripción: los eventos customer.subscription.* pueden llegar
                // ANTES que checkout.session.completed, cuando todavía no conocemos el customer.
                Metadata = new Dictionary<string, string>
                {
                    ["plan"] = plan.ToString(),
                    ["affiliateId"] = affiliateId.ToString(),
                },
            },
        };

        if (!string.IsNullOrEmpty(affiliate.StripeCustomerId))
            options.Customer = affiliate.StripeCustomerId;
        else if (!string.IsNullOrEmpty(affiliate.ContactEmail))
            options.CustomerEmail = affiliate.ContactEmail;

        var session = await new SessionService().CreateAsync(options);
        return new CheckoutSessionResponseDto(session.Url);
    }

    public async Task<PortalSessionResponseDto> CreatePortalSessionAsync(Guid affiliateId, string returnUrl)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId)
            ?? throw new KeyNotFoundException("Affiliate not found");

        if (string.IsNullOrEmpty(affiliate.StripeCustomerId))
            throw new KeyNotFoundException("Este negocio todavía no tiene una suscripción con Stripe.");

        UseApiKey();
        var session = await new Stripe.BillingPortal.SessionService().CreateAsync(
            new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = affiliate.StripeCustomerId,
                ReturnUrl = returnUrl,
                Configuration = await EnsurePortalConfigurationAsync(),
            });

        return new PortalSessionResponseDto(session.Url);
    }

    /// <summary>
    /// El Billing Portal falla si la cuenta (en ese modo) nunca guardó su configuración en el
    /// Dashboard. Si no hay default, se usa (o se crea una vez) una configuración propia
    /// marcada metadata maalca=1: actualizar tarjeta, datos de facturación, historial de facturas y
    /// cancelar al final del período. Así test y live funcionan sin pasos manuales.
    /// Devuelve null cuando ya existe un default (Stripe lo usa solo).
    /// </summary>
    private static async Task<string?> EnsurePortalConfigurationAsync()
    {
        var service = new Stripe.BillingPortal.ConfigurationService();
        var list = await service.ListAsync(new Stripe.BillingPortal.ConfigurationListOptions { Active = true, Limit = 20 });
        if (list.Data.Any(c => c.IsDefault)) return null;

        var ours = list.Data.FirstOrDefault(c => c.Metadata is not null
            && c.Metadata.TryGetValue("maalca", out var tag) && tag == "1");
        if (ours is not null) return ours.Id;

        var created = await service.CreateAsync(new Stripe.BillingPortal.ConfigurationCreateOptions
        {
            BusinessProfile = new Stripe.BillingPortal.ConfigurationBusinessProfileOptions { Headline = "MaalCa" },
            Features = new Stripe.BillingPortal.ConfigurationFeaturesOptions
            {
                InvoiceHistory = new Stripe.BillingPortal.ConfigurationFeaturesInvoiceHistoryOptions { Enabled = true },
                PaymentMethodUpdate = new Stripe.BillingPortal.ConfigurationFeaturesPaymentMethodUpdateOptions { Enabled = true },
                CustomerUpdate = new Stripe.BillingPortal.ConfigurationFeaturesCustomerUpdateOptions
                {
                    Enabled = true,
                    AllowedUpdates = new List<string> { "email", "address", "name" },
                },
                SubscriptionCancel = new Stripe.BillingPortal.ConfigurationFeaturesSubscriptionCancelOptions
                {
                    Enabled = true,
                    Mode = "at_period_end",
                },
            },
            Metadata = new Dictionary<string, string> { ["maalca"] = "1" },
        });
        return created.Id;
    }

    // ─────────────────────────────── Resumen y pantallas extra ───────────────────────────────

    public async Task<BillingSummaryDto> GetSummaryAsync(Guid affiliateId)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId)
            ?? throw new KeyNotFoundException("Affiliate not found");
        return await BuildSummaryAsync(affiliate);
    }

    private async Task<BillingSummaryDto> BuildSummaryAsync(Affiliate affiliate)
    {
        var used = await _db.Screens.CountAsync(s => s.AffiliateId == affiliate.Id);

        IReadOnlyList<BillingPriceDto> prices;
        try
        {
            prices = (await GetPricesAsync()).Values
                .Select(p => new BillingPriceDto(p.LookupKey, (p.UnitAmount ?? 0) / 100m, p.Recurring?.Interval))
                .OrderBy(p => p.LookupKey).ToList();
        }
        catch (StripeException)
        {
            prices = Array.Empty<BillingPriceDto>(); // sin Stripe el dashboard igual muestra el plan
        }

        return new BillingSummaryDto(
            Plan: affiliate.Plan.ToString(),
            PlanName: PublicPlanName(affiliate.Plan),
            PlanStatus: affiliate.PlanStatus.ToString(),
            Interval: affiliate.BillingInterval,
            HasSubscription: HasLiveSubscription(affiliate),
            IncludedScreens: _planLimit.GetIncludedScreens(affiliate.Plan),
            ExtraScreens: affiliate.ExtraScreens,
            ScreenAllowance: _planLimit.GetScreenAllowance(affiliate),
            ScreensUsed: used,
            SetupFeeDue: affiliate.SetupFeePaidAt is null,
            Prices: prices);
    }

    public async Task<BillingSummaryDto> UpdateExtraScreensAsync(Guid affiliateId, int extraScreens)
    {
        var affiliate = await _db.Affiliates.FindAsync(affiliateId)
            ?? throw new KeyNotFoundException("Affiliate not found");
        if (!HasLiveSubscription(affiliate))
            throw new InvalidOperationException(NoSubscriptionMessage);

        var qty = Math.Clamp(extraScreens, 0, MaxExtraScreens);
        var used = await _db.Screens.CountAsync(s => s.AffiliateId == affiliateId);
        var included = _planLimit.GetIncludedScreens(affiliate.Plan);
        if (used > included + qty)
            throw new ArgumentException($"Usas {used} pantallas: borra {used - included - qty} antes de bajar a {qty} extra.");

        UseApiKey();
        var subscription = await new SubscriptionService().GetAsync(affiliate.StripeSubscriptionId);
        var items = subscription.Items?.Data ?? new List<SubscriptionItem>();
        var planItem = items.FirstOrDefault(i => PlanFromLookupKey(i.Price?.LookupKey) is not null);
        var interval = planItem?.Price?.Recurring?.Interval ?? affiliate.BillingInterval ?? "month";
        var extraItem = items.FirstOrDefault(i => IsExtraScreenKey(i.Price?.LookupKey));

        // always_invoice: el prorrateo se cobra ya — la pantalla nueva queda pagada al crearla.
        const string proration = "always_invoice";
        var itemService = new SubscriptionItemService();
        if (extraItem is null && qty > 0)
        {
            await itemService.CreateAsync(new SubscriptionItemCreateOptions
            {
                Subscription = subscription.Id,
                Price = (await RequirePriceAsync(ExtraScreenKey(interval))).Id,
                Quantity = qty,
                ProrationBehavior = proration,
            });
        }
        else if (extraItem is not null && qty == 0)
        {
            await itemService.DeleteAsync(extraItem.Id, new SubscriptionItemDeleteOptions { ProrationBehavior = proration });
        }
        else if (extraItem is not null && extraItem.Quantity != qty)
        {
            await itemService.UpdateAsync(extraItem.Id, new SubscriptionItemUpdateOptions
            {
                Quantity = qty,
                ProrationBehavior = proration,
            });
        }

        // El webhook customer.subscription.updated confirmará lo mismo; actualizar ya evita que el
        // dashboard muestre el límite viejo mientras llega.
        affiliate.ExtraScreens = qty;
        await _db.SaveChangesAsync();
        return await BuildSummaryAsync(affiliate);
    }

    // ─────────────────────────────── Webhook ───────────────────────────────

    public async Task HandleWebhookEventAsync(string json, string signatureHeader)
    {
        var webhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? "";
        var stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, webhookSecret);

        // Idempotencia: Stripe reintenta y puede entregar el mismo evento más de una vez.
        if (await _db.StripeProcessedEvents.AnyAsync(e => e.EventId == stripeEvent.Id))
            return;

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await HandleCheckoutSessionCompletedAsync(stripeEvent);
                break;

            case "customer.subscription.created":
            case "customer.subscription.updated":
                if (stripeEvent.Data.Object is Subscription sub)
                    await SyncSubscriptionAsync(sub);
                break;

            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync(stripeEvent);
                break;

            case "invoice.payment_failed":
                await HandleInvoiceAsync(stripeEvent, paid: false);
                break;

            case "invoice.paid":
                await HandleInvoiceAsync(stripeEvent, paid: true);
                break;
        }

        _db.StripeProcessedEvents.Add(new StripeProcessedEvent
        {
            EventId = stripeEvent.Id,
            ProcessedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private async Task<Affiliate?> FindAffiliateAsync(IDictionary<string, string>? metadata, string? customerId, string? subscriptionId)
    {
        if (metadata is not null && metadata.TryGetValue("affiliateId", out var raw) && Guid.TryParse(raw, out var id))
        {
            var byId = await _db.Affiliates.FindAsync(id);
            if (byId is not null) return byId;
        }
        if (!string.IsNullOrEmpty(subscriptionId))
        {
            var bySub = await _db.Affiliates.FirstOrDefaultAsync(a => a.StripeSubscriptionId == subscriptionId);
            if (bySub is not null) return bySub;
        }
        if (!string.IsNullOrEmpty(customerId))
            return await _db.Affiliates.FirstOrDefaultAsync(a => a.StripeCustomerId == customerId);
        return null;
    }

    private async Task HandleCheckoutSessionCompletedAsync(Event stripeEvent)
    {
        if (stripeEvent.Data.Object is not Session session || session.Mode != "subscription") return;
        if (!Guid.TryParse(session.ClientReferenceId, out var affiliateId)) return;

        var affiliate = await _db.Affiliates.FindAsync(affiliateId);
        if (affiliate is null) return;

        var meta = session.Metadata ?? new Dictionary<string, string>();
        affiliate.Plan = ParsePlan(meta.GetValueOrDefault("plan"));
        affiliate.BillingInterval = ParseInterval(meta.GetValueOrDefault("interval"));
        affiliate.PlanStatus = PlanStatus.Active;
        affiliate.PlanStartedAt ??= DateTime.UtcNow;
        if (meta.GetValueOrDefault("setup") == "1" && session.PaymentStatus == "paid")
            affiliate.SetupFeePaidAt ??= DateTime.UtcNow;
        if (!string.IsNullOrEmpty(session.SubscriptionId))
            affiliate.StripeSubscriptionId = session.SubscriptionId;
        if (!string.IsNullOrEmpty(session.CustomerId))
            affiliate.StripeCustomerId = session.CustomerId;
    }

    private async Task SyncSubscriptionAsync(Subscription subscription)
    {
        var affiliate = await FindAffiliateAsync(subscription.Metadata, subscription.CustomerId, subscription.Id);
        if (affiliate is null) return;

        // Un evento tardío de una suscripción vieja (ya reemplazada) no debe pisar la actual.
        if (!string.IsNullOrEmpty(affiliate.StripeSubscriptionId) && affiliate.StripeSubscriptionId != subscription.Id
            && subscription.Status is "canceled" or "incomplete_expired")
            return;

        affiliate.StripeSubscriptionId = subscription.Id;
        if (string.IsNullOrEmpty(affiliate.StripeCustomerId))
            affiliate.StripeCustomerId = subscription.CustomerId;

        var extras = 0;
        foreach (var item in subscription.Items?.Data ?? new List<SubscriptionItem>())
        {
            var key = item.Price?.LookupKey;
            if (PlanFromLookupKey(key) is { } plan)
            {
                affiliate.Plan = plan;
                affiliate.BillingInterval = item.Price?.Recurring?.Interval ?? affiliate.BillingInterval;
            }
            else if (IsExtraScreenKey(key))
            {
                extras += (int)item.Quantity;
            }
        }
        affiliate.ExtraScreens = extras;

        affiliate.PlanStatus = subscription.Status switch
        {
            "active" or "trialing" => PlanStatus.Active,
            "past_due" => PlanStatus.PastDue,
            "canceled" or "unpaid" or "incomplete_expired" => PlanStatus.Canceled,
            _ => affiliate.PlanStatus // "incomplete": esperando el primer pago, no cambia nada todavía
        };
        if (affiliate.PlanStatus == PlanStatus.Active)
            affiliate.PlanStartedAt ??= DateTime.UtcNow;
    }

    private async Task HandleSubscriptionDeletedAsync(Event stripeEvent)
    {
        if (stripeEvent.Data.Object is not Subscription subscription) return;

        var affiliate = await FindAffiliateAsync(subscription.Metadata, subscription.CustomerId, subscription.Id);
        if (affiliate is null) return;
        if (!string.IsNullOrEmpty(affiliate.StripeSubscriptionId) && affiliate.StripeSubscriptionId != subscription.Id)
            return; // se canceló una vieja; la actual sigue

        affiliate.Plan = Plan.Free;
        affiliate.PlanStatus = PlanStatus.Canceled;
        affiliate.ExtraScreens = 0;
        affiliate.BillingInterval = null;
        affiliate.StripeSubscriptionId = null; // permite volver a suscribirse por Checkout
    }

    /// <summary>
    /// invoice.payment_failed → PastDue (el dashboard muestra el aviso para actualizar la tarjeta;
    /// Stripe sigue reintentando). invoice.paid → vuelve a Active si estaba PastDue.
    /// </summary>
    private async Task HandleInvoiceAsync(Event stripeEvent, bool paid)
    {
        if (stripeEvent.Data.Object is not StripeInvoice invoice || string.IsNullOrEmpty(invoice.CustomerId)) return;

        var affiliate = await _db.Affiliates.FirstOrDefaultAsync(a => a.StripeCustomerId == invoice.CustomerId);
        if (affiliate is null || affiliate.Plan == Plan.Free) return;

        if (!paid && affiliate.PlanStatus == PlanStatus.Active)
            affiliate.PlanStatus = PlanStatus.PastDue;
        else if (paid && affiliate.PlanStatus == PlanStatus.PastDue)
            affiliate.PlanStatus = PlanStatus.Active;
    }
}
