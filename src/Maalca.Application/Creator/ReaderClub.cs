using System.Text;
using System.Text.RegularExpressions;
using Maalca.Application.Common;
using Maalca.Domain.Entities;
using Maalca.Domain.Enums;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;

namespace Maalca.Application.Creator;

// Club de lectores del espacio de autor (ver ReaderClub.cs en Domain):
//   - Cartas: el lector deja su correo, confirma (doble opt-in) y recibe lo que el autor escribe en
//     /space/[slug]/readers. Cada carta lleva su enlace de baja firmado.
//   - Entrar: sin contraseña. Un enlace firmado llega al correo; la web guarda la sesión en una cookie
//     httpOnly y la manda al API en X-Reader-Token.
//   - Membresía: suscripción mensual en la cuenta Stripe Connect del autor (cargo directo). Abre las obras
//     en ReadingAccess "members". Stripe manda; webhook + sincronización al volver del Checkout.
//   - Seguir leyendo: último capítulo abierto por obra, guardado en el servidor.

// ── DTOs ─────────────────────────────────────────────────────────────────────────

public record ReaderContext(Guid ReaderId, bool IsMember);

public record ReaderSubscribeRequest(string Email, string? Name, string? Language, string? WorkSlug, string? Source);
public record ReaderEmailRequest(string Email, string? Language);
public record ReaderTokenRequest(string Token);
public record ReaderMeDto(string Email, string? Name, string Status, bool LettersOptIn, bool IsMember, string MembershipStatus, DateTime? MembershipCurrentPeriodEnd);
public record ReaderSessionDto(string SessionToken, ReaderMeDto Me);
public record ReaderUnsubscribeResultDto(string Email, bool StillMember);

public record PublicReaderClubDto(
    bool LettersEnabled,
    string? LettersPitch,
    string? LettersPitchEn,
    bool MembershipAvailable,
    decimal? MonthlyPrice,
    string Currency,
    string? Benefits,
    string? BenefitsEn);

public record MembershipCheckoutRequest(string? Email, string SuccessUrl, string CancelUrl, string? Language);
public record ReaderUrlDto(string Url);
public record MembershipSyncRequest(string SessionId);
public record MembershipSyncDto(bool Active, string? SessionToken);
public record ReaderPortalRequest(string ReturnUrl);
public record SaveReaderProgressRequest(string WorkSlug, int ChapterNumber);

public record ReaderClubSettingsDto(
    bool LettersEnabled,
    string? LettersPitch,
    string? LettersPitchEn,
    bool MembershipEnabled,
    decimal? MonthlyPrice,
    string Currency,
    string? Benefits,
    string? BenefitsEn,
    bool PaymentsReady);

public record UpdateReaderClubSettingsRequest(
    bool LettersEnabled,
    string? LettersPitch,
    string? LettersPitchEn,
    bool MembershipEnabled,
    decimal? MonthlyPrice,
    string? Currency,
    string? Benefits,
    string? BenefitsEn);

public record ReaderStatsDto(int Confirmed, int Pending, int Members, int LettersOptIn, int Unsubscribed);

public record ReaderRowDto(
    Guid Id,
    string Email,
    string? Name,
    string Status,
    bool LettersOptIn,
    string MembershipStatus,
    bool IsMember,
    DateTime? MembershipCurrentPeriodEnd,
    string Source,
    Guid? SourceWorkId,
    string? SourceWorkTitle,
    DateTime CreatedAt,
    DateTime? ConfirmedAt);

public record ReaderListDto(ReaderStatsDto Stats, IReadOnlyList<ReaderRowDto> Items, int Total);

public record ReaderLetterDto(
    Guid Id,
    string Subject,
    string? BodyHtml,
    string Audience,
    string Status,
    DateTime? QueuedAt,
    DateTime? SentAt,
    int RecipientCount,
    int SentCount,
    int FailedCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record UpsertReaderLetterRequest(string Subject, string? BodyHtml, string? Audience);
public record SendTestLetterRequest(string? Email);

// ── Contrato ─────────────────────────────────────────────────────────────────────

public interface IReaderClubService
{
    // Público (página del autor). ArgumentException = datos inválidos (400). null = autor inexistente.
    Task<PublicReaderClubDto?> GetPublicClubAsync(string slug);
    Task<bool?> SubscribeAsync(string slug, ReaderSubscribeRequest request);
    Task<bool?> RequestLoginAsync(string slug, ReaderEmailRequest request);
    Task<ReaderSessionDto?> ConfirmAsync(string slug, Guid confirmToken);
    Task<ReaderSessionDto?> VerifyLoginAsync(string slug, string loginToken);
    Task<ReaderUnsubscribeResultDto?> UnsubscribeAsync(string slug, string unsubscribeToken);
    Task<ReaderContext?> ResolveAsync(string slug, string? sessionToken);
    Task<ReaderMeDto?> GetMeAsync(string slug, string? sessionToken);
    Task<bool> SaveProgressAsync(string slug, string? sessionToken, SaveReaderProgressRequest request);
    Task<ReaderUrlDto?> CreateMembershipCheckoutAsync(string slug, string? sessionToken, MembershipCheckoutRequest request);
    Task<MembershipSyncDto?> SyncMembershipCheckoutAsync(string slug, string? sessionToken, MembershipSyncRequest request);
    Task<ReaderUrlDto?> CreatePortalAsync(string slug, string? sessionToken, ReaderPortalRequest request);

    // Webhook de Connect (lo llama StripeConnectService). No-op si el evento no es del club.
    Task HandleStripeEventAsync(Event stripeEvent);

    // Dashboard del autor.
    Task<ReaderClubSettingsDto> GetSettingsAsync(Guid affiliateId);
    Task<ReaderClubSettingsDto> UpdateSettingsAsync(Guid affiliateId, UpdateReaderClubSettingsRequest request);
    Task<ReaderListDto> ListReadersAsync(Guid affiliateId, string? filter, string? search, int skip, int take);
    Task<string> ExportCsvAsync(Guid affiliateId);
    Task<bool> DeleteReaderAsync(Guid affiliateId, Guid readerId);
    Task<List<ReaderLetterDto>> ListLettersAsync(Guid affiliateId);
    Task<ReaderLetterDto> CreateLetterAsync(Guid affiliateId, UpsertReaderLetterRequest request);
    Task<ReaderLetterDto?> UpdateLetterAsync(Guid affiliateId, Guid letterId, UpsertReaderLetterRequest request);
    Task<bool> DeleteLetterAsync(Guid affiliateId, Guid letterId);
    Task<ReaderLetterDto?> QueueLetterAsync(Guid affiliateId, Guid letterId);
    Task<bool?> SendTestLetterAsync(Guid affiliateId, Guid letterId, SendTestLetterRequest request);

    // Worker de cartas.
    Task<int> ProcessQueuedLettersAsync(CancellationToken ct);
}

// ── Servicio ─────────────────────────────────────────────────────────────────────

public sealed class ReaderClubService : IReaderClubService
{
    private const string MembershipKind = "reader_membership";
    private const int LetterBatch = 50;
    private static readonly TimeSpan MailCooldown = TimeSpan.FromSeconds(60);
    private static readonly BusinessType[] AuthorTypes = { BusinessType.Creator, BusinessType.Publisher };
    private static readonly Regex EmailRx = new(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly ReaderTokens _tokens;
    private readonly IReaderMailer _mailer;
    private readonly ILogger<ReaderClubService> _logger;

    public ReaderClubService(AppDbContext db, ReaderTokens tokens, IReaderMailer mailer, ILogger<ReaderClubService> logger)
    {
        _db = db;
        _tokens = tokens;
        _mailer = mailer;
        _logger = logger;
    }

    // ── Regla de miembro ─────────────────────────────────────────────────────────

    /// <summary>Activo, o atrasado en el pago pero todavía dentro del período pagado.</summary>
    public static bool IsMember(Reader r, DateTime nowUtc)
        => r.MembershipStatus == "active"
           || (r.MembershipStatus == "past_due" && r.MembershipCurrentPeriodEnd is { } end && end > nowUtc);

    // ── Público ──────────────────────────────────────────────────────────────────

    public async Task<PublicReaderClubDto?> GetPublicClubAsync(string slug)
    {
        var author = await AuthorAsync(slug);
        if (author is null) return null;
        var s = await SettingsAsync(author.Id, track: false);
        var available = MembershipAvailable(author, s);
        return new PublicReaderClubDto(
            s.LettersEnabled, s.LettersPitch, s.LettersPitchEn,
            available, available ? s.MembershipMonthlyPrice : null, s.MembershipCurrency,
            available ? s.MembershipBenefits : null, available ? s.MembershipBenefitsEn : null);
    }

    public async Task<bool?> SubscribeAsync(string slug, ReaderSubscribeRequest r)
    {
        var author = await AuthorAsync(slug);
        if (author is null) return null;
        var s = await SettingsAsync(author.Id, track: false);
        if (!s.LettersEnabled) throw new ArgumentException("Este autor no está recibiendo suscripciones ahora.");

        var email = NormalizeEmail(r.Email);
        var lang = Lang(r.Language);
        Guid? workId = null;
        if (!string.IsNullOrWhiteSpace(r.WorkSlug))
        {
            var ws = r.WorkSlug.Trim().ToLowerInvariant();
            workId = await _db.Works.Where(w => w.AffiliateId == author.Id && w.Slug == ws).Select(w => (Guid?)w.Id).FirstOrDefaultAsync();
        }

        var reader = await _db.Readers.FirstOrDefaultAsync(x => x.AffiliateId == author.Id && x.Email == email);
        var now = DateTime.UtcNow;
        if (reader is null)
        {
            reader = new Reader
            {
                Id = Guid.NewGuid(), AffiliateId = author.Id, Email = email, Name = Clip(r.Name, 120), Language = lang,
                Status = "pending", LettersOptIn = true, ConfirmToken = Guid.NewGuid(),
                Source = workId.HasValue ? "work" : Token(r.Source, new[] { "page", "work", "reader" }, "page"),
                SourceWorkId = workId, CreatedAt = now,
            };
            _db.Readers.Add(reader);
            await _db.SaveChangesAsync();
            await SendConfirmAsync(author, reader);
            return true;
        }

        if (Cooling(reader, now)) return true; // ya se le mandó algo hace menos de un minuto: misma respuesta, sin otro correo

        if (reader.Status == "confirmed")
        {
            // Ya está dentro. Si se había bajado solo de las cartas, apuntarse de nuevo lo vuelve a activar:
            // lo pidió él, con su correo, y el enlace para entrar confirma que el correo es suyo.
            if (!reader.LettersOptIn) reader.LettersOptIn = true;
            reader.UpdatedAt = now;
            await _db.SaveChangesAsync();
            await SendLoginAsync(author, reader);
            return true;
        }

        // pending o unsubscribed: vuelve a pedir confirmación con un token nuevo.
        reader.Status = "pending";
        reader.LettersOptIn = true;
        reader.ConfirmToken = Guid.NewGuid();
        reader.Language = lang;
        if (!string.IsNullOrWhiteSpace(r.Name)) reader.Name = Clip(r.Name, 120);
        reader.UpdatedAt = now;
        await _db.SaveChangesAsync();
        await SendConfirmAsync(author, reader);
        return true;
    }

    public async Task<bool?> RequestLoginAsync(string slug, ReaderEmailRequest r)
    {
        var author = await AuthorAsync(slug);
        if (author is null) return null;
        var email = NormalizeEmail(r.Email);
        var reader = await _db.Readers.FirstOrDefaultAsync(x => x.AffiliateId == author.Id && x.Email == email);
        var now = DateTime.UtcNow;
        // Respuesta igual exista o no: no se revela quién está suscrito.
        if (reader is null || Cooling(reader, now)) return true;

        reader.UpdatedAt = now;
        await _db.SaveChangesAsync();
        if (reader.Status == "confirmed" || IsMember(reader, now))
            await SendLoginAsync(author, reader);
        else
            await SendConfirmAsync(author, reader);
        return true;
    }

    public async Task<ReaderSessionDto?> ConfirmAsync(string slug, Guid confirmToken)
    {
        var author = await AuthorAsync(slug);
        if (author is null) return null;
        var reader = await _db.Readers.FirstOrDefaultAsync(x => x.AffiliateId == author.Id && x.ConfirmToken == confirmToken);
        if (reader is null) return null;

        var now = DateTime.UtcNow;
        if (reader.Status != "confirmed")
        {
            reader.Status = "confirmed";
            reader.ConfirmedAt = now;
            reader.UnsubscribedAt = null;
        }
        reader.LettersOptIn = true;
        // El token de confirmación es de un solo uso: se gira al usarlo.
        reader.ConfirmToken = Guid.NewGuid();
        reader.LastLoginAt = now;
        reader.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return new ReaderSessionDto(_tokens.Session(reader.Id, author.Id), ToMe(reader, now));
    }

    public async Task<ReaderSessionDto?> VerifyLoginAsync(string slug, string loginToken)
    {
        var author = await AuthorAsync(slug);
        if (author is null) return null;
        var t = _tokens.Read(loginToken, "login");
        if (t is null || t.Value.AffiliateId != author.Id) return null;
        var reader = await _db.Readers.FirstOrDefaultAsync(x => x.Id == t.Value.ReaderId && x.AffiliateId == author.Id);
        if (reader is null) return null;

        var now = DateTime.UtcNow;
        // Abrir el enlace de su correo también prueba que el correo es suyo.
        if (reader.Status != "confirmed")
        {
            reader.Status = "confirmed";
            reader.ConfirmedAt ??= now;
            reader.UnsubscribedAt = null;
        }
        reader.LastLoginAt = now;
        reader.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return new ReaderSessionDto(_tokens.Session(reader.Id, author.Id), ToMe(reader, now));
    }

    public async Task<ReaderUnsubscribeResultDto?> UnsubscribeAsync(string slug, string unsubscribeToken)
    {
        var author = await AuthorAsync(slug);
        if (author is null) return null;
        var t = _tokens.Read(unsubscribeToken, "unsub");
        if (t is null || t.Value.AffiliateId != author.Id) return null;
        var reader = await _db.Readers.FirstOrDefaultAsync(x => x.Id == t.Value.ReaderId && x.AffiliateId == author.Id);
        if (reader is null) return null;

        var now = DateTime.UtcNow;
        var member = IsMember(reader, now);
        reader.LettersOptIn = false;
        reader.UnsubscribedAt = now;
        // Un miembro se baja de las cartas, no de su membresía (esa se cancela en el portal de Stripe).
        if (!member) reader.Status = "unsubscribed";
        reader.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return new ReaderUnsubscribeResultDto(reader.Email, member);
    }

    public async Task<ReaderContext?> ResolveAsync(string slug, string? sessionToken)
    {
        var reader = await SessionReaderAsync(slug, sessionToken);
        return reader is null ? null : new ReaderContext(reader.Id, IsMember(reader, DateTime.UtcNow));
    }

    public async Task<ReaderMeDto?> GetMeAsync(string slug, string? sessionToken)
    {
        var reader = await SessionReaderAsync(slug, sessionToken);
        return reader is null ? null : ToMe(reader, DateTime.UtcNow);
    }

    public async Task<bool> SaveProgressAsync(string slug, string? sessionToken, SaveReaderProgressRequest r)
    {
        var reader = await SessionReaderAsync(slug, sessionToken, track: false);
        if (reader is null || r.ChapterNumber < 1) return false;
        var ws = (r.WorkSlug ?? string.Empty).Trim().ToLowerInvariant();
        var workId = await _db.Works.Where(w => w.AffiliateId == reader.AffiliateId && w.Slug == ws).Select(w => (Guid?)w.Id).FirstOrDefaultAsync();
        if (workId is null) return false;

        var now = DateTime.UtcNow;
        var row = await _db.ReaderProgress.FirstOrDefaultAsync(p => p.ReaderId == reader.Id && p.WorkId == workId);
        if (row is null)
            _db.ReaderProgress.Add(new ReaderProgress { Id = Guid.NewGuid(), ReaderId = reader.Id, WorkId = workId.Value, ChapterNumber = r.ChapterNumber, CreatedAt = now });
        else
        {
            row.ChapterNumber = r.ChapterNumber;
            row.UpdatedAt = now;
        }
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return false; } // dos pestañas a la vez: gana la primera, no importa
        return true;
    }

    // ── Membresía (Stripe Connect del autor) ─────────────────────────────────────

    public async Task<ReaderUrlDto?> CreateMembershipCheckoutAsync(string slug, string? sessionToken, MembershipCheckoutRequest r)
    {
        var author = await AuthorAsync(slug, track: true);
        if (author is null) return null;
        var s = await SettingsAsync(author.Id, track: true);
        if (!MembershipAvailable(author, s)) throw new ArgumentException("La membresía no está disponible ahora.");
        if (!IsHttpUrl(r.SuccessUrl) || !IsHttpUrl(r.CancelUrl)) throw new ArgumentException("URLs de regreso inválidas.");

        var now = DateTime.UtcNow;
        var reader = await SessionReaderAsync(slug, sessionToken, track: true);
        if (reader is null)
        {
            var email = NormalizeEmail(r.Email ?? string.Empty);
            reader = await _db.Readers.FirstOrDefaultAsync(x => x.AffiliateId == author.Id && x.Email == email);
            if (reader is null)
            {
                reader = new Reader
                {
                    Id = Guid.NewGuid(), AffiliateId = author.Id, Email = email, Language = Lang(r.Language),
                    Status = "pending", LettersOptIn = true, ConfirmToken = Guid.NewGuid(), Source = "checkout", CreatedAt = now,
                };
                _db.Readers.Add(reader);
                await _db.SaveChangesAsync();
            }
        }
        if (IsMember(reader, now)) throw new ArgumentException("Ya tienes la membresía activa. Entra con tu correo para leer.");

        var opts = Connect(author);
        var priceId = await EnsurePriceAsync(author, s, opts);

        var meta = new Dictionary<string, string>
        {
            ["kind"] = MembershipKind,
            ["readerId"] = reader.Id.ToString(),
            ["affiliateId"] = author.Id.ToString(),
        };
        var session = await new SessionService().CreateAsync(new SessionCreateOptions
        {
            Mode = "subscription",
            LineItems = new List<SessionLineItemOptions> { new() { Price = priceId, Quantity = 1 } },
            SuccessUrl = r.SuccessUrl,
            CancelUrl = r.CancelUrl,
            ClientReferenceId = reader.Id.ToString(),
            Customer = reader.StripeCustomerId,
            CustomerEmail = reader.StripeCustomerId is null ? reader.Email : null,
            Locale = reader.Language == "en" ? "en" : "es",
            Metadata = meta,
            SubscriptionData = new SessionSubscriptionDataOptions { Metadata = meta },
        }, opts);

        return new ReaderUrlDto(session.Url);
    }

    public async Task<MembershipSyncDto?> SyncMembershipCheckoutAsync(string slug, string? sessionToken, MembershipSyncRequest r)
    {
        var author = await AuthorAsync(slug);
        if (author is null || string.IsNullOrWhiteSpace(r.SessionId) || !r.SessionId.StartsWith("cs_")) return null;
        if (string.IsNullOrEmpty(author.StripeConnectAccountId)) return null;

        var opts = Connect(author);
        Session session;
        try
        {
            session = await new SessionService().GetAsync(r.SessionId, new SessionGetOptions { Expand = new List<string> { "subscription" } }, opts);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "[ReaderClub] no se pudo leer el checkout {Session}", r.SessionId);
            return null;
        }
        if (session.Metadata?.GetValueOrDefault("kind") != MembershipKind
            || session.Metadata.GetValueOrDefault("affiliateId") != author.Id.ToString())
            return null;

        var reader = await ApplyCheckoutAsync(session);
        if (reader is null) return new MembershipSyncDto(false, null);

        var now = DateTime.UtcNow;
        // Sesión solo si ya era él (traía su sesión) o si el lector nació en este checkout y nunca había entrado:
        // así un correo ajeno ya existente no queda abierto para quien tenga el id del checkout.
        var caller = await SessionReaderAsync(slug, sessionToken, track: false);
        var issue = caller?.Id == reader.Id || (reader.Source == "checkout" && reader.LastLoginAt is null);
        string? token = null;
        if (issue)
        {
            reader.LastLoginAt = now;
            await _db.SaveChangesAsync();
            token = _tokens.Session(reader.Id, author.Id);
        }
        return new MembershipSyncDto(IsMember(reader, now), token);
    }

    public async Task<ReaderUrlDto?> CreatePortalAsync(string slug, string? sessionToken, ReaderPortalRequest r)
    {
        var reader = await SessionReaderAsync(slug, sessionToken, track: false);
        if (reader is null || string.IsNullOrEmpty(reader.StripeCustomerId)) return null;
        if (!IsHttpUrl(r.ReturnUrl)) throw new ArgumentException("URL de regreso inválida.");
        var author = await _db.Affiliates.FirstAsync(a => a.Id == reader.AffiliateId);
        if (string.IsNullOrEmpty(author.StripeConnectAccountId)) return null;

        var opts = Connect(author);
        var portal = await new Stripe.BillingPortal.SessionService().CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = reader.StripeCustomerId,
            ReturnUrl = r.ReturnUrl,
            Configuration = await EnsurePortalConfigurationAsync(author.Name, opts),
        }, opts);
        return new ReaderUrlDto(portal.Url);
    }

    public async Task HandleStripeEventAsync(Event stripeEvent)
    {
        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                if (stripeEvent.Data.Object is Session session && session.Mode == "subscription"
                    && session.Metadata?.GetValueOrDefault("kind") == MembershipKind)
                {
                    // El objeto del evento no trae la suscripción expandida: se pide a la cuenta del evento.
                    if (!string.IsNullOrEmpty(stripeEvent.Account) && !string.IsNullOrEmpty(session.SubscriptionId))
                    {
                        try
                        {
                            session.Subscription = await new SubscriptionService().GetAsync(session.SubscriptionId, null,
                                new RequestOptions { StripeAccount = stripeEvent.Account, ApiKey = ApiKey() });
                        }
                        catch (StripeException ex) { _logger.LogWarning(ex, "[ReaderClub] suscripción {Sub} no legible", session.SubscriptionId); }
                    }
                    await ApplyCheckoutAsync(session);
                }
                break;
            case "customer.subscription.created":
            case "customer.subscription.updated":
            case "customer.subscription.deleted":
                if (stripeEvent.Data.Object is Subscription sub && sub.Metadata?.GetValueOrDefault("kind") == MembershipKind)
                    await ApplySubscriptionAsync(sub, readerHint: null);
                break;
        }
    }

    private async Task<Reader?> ApplyCheckoutAsync(Session session)
    {
        if (session.Status != "complete") return null;
        if (!Guid.TryParse(session.Metadata?.GetValueOrDefault("readerId"), out var readerId)) return null;
        var reader = await _db.Readers.FirstOrDefaultAsync(x => x.Id == readerId);
        if (reader is null) return null;

        reader.StripeCustomerId ??= session.CustomerId;
        if (session.Subscription is not null)
            await ApplySubscriptionAsync(session.Subscription, reader);
        else
        {
            reader.StripeSubscriptionId ??= session.SubscriptionId;
            reader.MembershipStatus = "active";
            reader.MembershipStartedAt ??= DateTime.UtcNow;
            ConfirmByPayment(reader);
            await _db.SaveChangesAsync();
        }
        return reader;
    }

    private async Task ApplySubscriptionAsync(Subscription sub, Reader? readerHint)
    {
        var reader = readerHint;
        if (reader is null && Guid.TryParse(sub.Metadata?.GetValueOrDefault("readerId"), out var rid))
            reader = await _db.Readers.FirstOrDefaultAsync(x => x.Id == rid);
        reader ??= await _db.Readers.FirstOrDefaultAsync(x => x.StripeSubscriptionId == sub.Id);
        if (reader is null) return;

        // Una suscripción vieja cancelada no pisa a la nueva.
        if (!string.IsNullOrEmpty(reader.StripeSubscriptionId) && reader.StripeSubscriptionId != sub.Id
            && sub.Status is "canceled" or "incomplete_expired")
            return;

        reader.StripeSubscriptionId = sub.Id;
        reader.StripeCustomerId ??= sub.CustomerId;
        var status = sub.Status switch
        {
            "active" or "trialing" => "active",
            "past_due" => "past_due",
            "canceled" or "unpaid" or "incomplete_expired" => "canceled",
            _ => reader.MembershipStatus, // "incomplete": esperando el primer pago
        };
        reader.MembershipStatus = status;
        var periodEnd = sub.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd;
        if (periodEnd is { } pe && pe > DateTime.MinValue) reader.MembershipCurrentPeriodEnd = DateTime.SpecifyKind(pe, DateTimeKind.Utc);
        if (status == "active")
        {
            reader.MembershipStartedAt ??= DateTime.UtcNow;
            ConfirmByPayment(reader);
        }
        reader.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private static void ConfirmByPayment(Reader reader)
    {
        if (reader.Status == "confirmed") return;
        reader.Status = "confirmed";
        reader.ConfirmedAt ??= DateTime.UtcNow;
        reader.UnsubscribedAt = null;
    }

    private async Task<string> EnsurePriceAsync(Affiliate author, ReaderClubSettings s, RequestOptions opts)
    {
        var amount = s.MembershipMonthlyPrice!.Value;
        var currency = s.MembershipCurrency;
        if (!string.IsNullOrEmpty(s.StripePriceId) && s.StripePriceAmount == amount && s.StripePriceCurrency == currency)
            return s.StripePriceId;

        if (string.IsNullOrEmpty(s.StripeProductId))
        {
            var product = await new Stripe.ProductService().CreateAsync(new ProductCreateOptions
            {
                Name = $"Membresía — {author.Name}",
                Metadata = new Dictionary<string, string> { ["kind"] = MembershipKind, ["affiliateId"] = author.Id.ToString() },
            }, opts);
            s.StripeProductId = product.Id;
        }

        var price = await new Stripe.PriceService().CreateAsync(new PriceCreateOptions
        {
            Product = s.StripeProductId,
            Currency = currency.ToLowerInvariant(),
            UnitAmount = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero),
            Recurring = new PriceRecurringOptions { Interval = "month" },
            Metadata = new Dictionary<string, string> { ["kind"] = MembershipKind },
        }, opts);

        s.StripePriceId = price.Id;
        s.StripePriceAmount = amount;
        s.StripePriceCurrency = currency;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return price.Id;
    }

    /// <summary>Mismo criterio que StripeBillingService: si la cuenta del autor no tiene portal por defecto, se crea uno propio.</summary>
    private static async Task<string?> EnsurePortalConfigurationAsync(string authorName, RequestOptions opts)
    {
        var service = new Stripe.BillingPortal.ConfigurationService();
        var list = await service.ListAsync(new Stripe.BillingPortal.ConfigurationListOptions { Active = true, Limit = 20 }, opts);
        if (list.Data.Any(c => c.IsDefault)) return null;
        var ours = list.Data.FirstOrDefault(c => c.Metadata is not null && c.Metadata.TryGetValue("maalca", out var tag) && tag == "readers");
        if (ours is not null) return ours.Id;

        var created = await service.CreateAsync(new Stripe.BillingPortal.ConfigurationCreateOptions
        {
            BusinessProfile = new Stripe.BillingPortal.ConfigurationBusinessProfileOptions { Headline = authorName },
            Features = new Stripe.BillingPortal.ConfigurationFeaturesOptions
            {
                InvoiceHistory = new Stripe.BillingPortal.ConfigurationFeaturesInvoiceHistoryOptions { Enabled = true },
                PaymentMethodUpdate = new Stripe.BillingPortal.ConfigurationFeaturesPaymentMethodUpdateOptions { Enabled = true },
                SubscriptionCancel = new Stripe.BillingPortal.ConfigurationFeaturesSubscriptionCancelOptions { Enabled = true, Mode = "at_period_end" },
            },
            Metadata = new Dictionary<string, string> { ["maalca"] = "readers" },
        }, opts);
        return created.Id;
    }

    // ── Dashboard ────────────────────────────────────────────────────────────────

    public async Task<ReaderClubSettingsDto> GetSettingsAsync(Guid affiliateId)
    {
        var author = await _db.Affiliates.AsNoTracking().FirstAsync(a => a.Id == affiliateId);
        return ToSettingsDto(await SettingsAsync(affiliateId, track: false), author);
    }

    public async Task<ReaderClubSettingsDto> UpdateSettingsAsync(Guid affiliateId, UpdateReaderClubSettingsRequest r)
    {
        var author = await _db.Affiliates.AsNoTracking().FirstAsync(a => a.Id == affiliateId);
        var s = await SettingsAsync(affiliateId, track: true);

        var currency = (r.Currency ?? "USD").Trim().ToUpperInvariant();
        if (currency is not ("USD" or "DOP")) throw new ArgumentException("Moneda no válida (USD o DOP).");
        if (r.MonthlyPrice is { } p && (p < 1m || p > 10_000m)) throw new ArgumentException("El precio mensual debe estar entre 1 y 10,000.");
        if (r.MembershipEnabled && r.MonthlyPrice is null) throw new ArgumentException("Pon el precio mensual para activar la membresía.");

        s.LettersEnabled = r.LettersEnabled;
        s.LettersPitch = Clip(r.LettersPitch, 600);
        s.LettersPitchEn = Clip(r.LettersPitchEn, 600);
        s.MembershipEnabled = r.MembershipEnabled;
        s.MembershipMonthlyPrice = r.MonthlyPrice is { } price ? Math.Round(price, 2) : null;
        s.MembershipCurrency = currency;
        s.MembershipBenefits = HtmlScrub.SanitizeRichText(Clip(r.Benefits, 5_000));
        s.MembershipBenefitsEn = HtmlScrub.SanitizeRichText(Clip(r.BenefitsEn, 5_000));
        s.UpdatedAt = DateTime.UtcNow;
        // El precio en Stripe se rehace en el próximo checkout (EnsurePriceAsync); los miembros actuales siguen con el suyo.
        await _db.SaveChangesAsync();
        return ToSettingsDto(s, author);
    }

    public async Task<ReaderListDto> ListReadersAsync(Guid affiliateId, string? filter, string? search, int skip, int take)
    {
        var now = DateTime.UtcNow;
        var all = _db.Readers.AsNoTracking().Where(x => x.AffiliateId == affiliateId);

        var stats = new ReaderStatsDto(
            await all.CountAsync(x => x.Status == "confirmed"),
            await all.CountAsync(x => x.Status == "pending"),
            await all.CountAsync(x => x.MembershipStatus == "active" || (x.MembershipStatus == "past_due" && x.MembershipCurrentPeriodEnd > now)),
            await all.CountAsync(x => x.Status == "confirmed" && x.LettersOptIn),
            await all.CountAsync(x => x.Status == "unsubscribed"));

        var q = all;
        q = (filter ?? "all") switch
        {
            "confirmed" => q.Where(x => x.Status == "confirmed"),
            "pending" => q.Where(x => x.Status == "pending"),
            "members" => q.Where(x => x.MembershipStatus == "active" || (x.MembershipStatus == "past_due" && x.MembershipCurrentPeriodEnd > now)),
            "unsubscribed" => q.Where(x => x.Status == "unsubscribed"),
            _ => q,
        };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(x => x.Email.Contains(term) || (x.Name != null && x.Name.ToLower().Contains(term)));
        }

        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(x => x.CreatedAt)
            .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200))
            .ToListAsync();
        var workIds = rows.Where(x => x.SourceWorkId.HasValue).Select(x => x.SourceWorkId!.Value).Distinct().ToList();
        var titles = await _db.Works.AsNoTracking().Where(w => workIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.Title);

        var items = rows.Select(x => new ReaderRowDto(
            x.Id, x.Email, x.Name, x.Status, x.LettersOptIn, x.MembershipStatus, IsMember(x, now), x.MembershipCurrentPeriodEnd,
            x.Source, x.SourceWorkId, x.SourceWorkId is { } wid && titles.TryGetValue(wid, out var t) ? t : null,
            x.CreatedAt, x.ConfirmedAt)).ToList();
        return new ReaderListDto(stats, items, total);
    }

    public async Task<string> ExportCsvAsync(Guid affiliateId)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.Readers.AsNoTracking().Where(x => x.AffiliateId == affiliateId).OrderBy(x => x.CreatedAt).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("email,nombre,estado,cartas,miembro,idioma,origen,alta,confirmado");
        foreach (var x in rows)
        {
            sb.Append(Csv(x.Email)).Append(',').Append(Csv(x.Name)).Append(',').Append(x.Status).Append(',')
              .Append(x.LettersOptIn ? "si" : "no").Append(',').Append(IsMember(x, now) ? "si" : "no").Append(',')
              .Append(x.Language).Append(',').Append(x.Source).Append(',')
              .Append(x.CreatedAt.ToString("yyyy-MM-dd")).Append(',').Append(x.ConfirmedAt?.ToString("yyyy-MM-dd") ?? "")
              .AppendLine();
        }
        return sb.ToString();
    }

    public async Task<bool> DeleteReaderAsync(Guid affiliateId, Guid readerId)
    {
        var reader = await _db.Readers.FirstOrDefaultAsync(x => x.Id == readerId && x.AffiliateId == affiliateId);
        if (reader is null) return false;
        if (IsMember(reader, DateTime.UtcNow))
            throw new ArgumentException("Este lector tiene la membresía activa. Cancélala primero en Stripe; después se puede borrar.");
        _db.Readers.Remove(reader);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<ReaderLetterDto>> ListLettersAsync(Guid affiliateId)
        => (await _db.ReaderLetters.AsNoTracking().Where(l => l.AffiliateId == affiliateId)
                .OrderByDescending(l => l.CreatedAt).Take(200).ToListAsync())
            .Select(ToLetterDto).ToList();

    public async Task<ReaderLetterDto> CreateLetterAsync(Guid affiliateId, UpsertReaderLetterRequest r)
    {
        var letter = new ReaderLetter { Id = Guid.NewGuid(), AffiliateId = affiliateId, CreatedAt = DateTime.UtcNow };
        ApplyLetter(letter, r);
        _db.ReaderLetters.Add(letter);
        await _db.SaveChangesAsync();
        return ToLetterDto(letter);
    }

    public async Task<ReaderLetterDto?> UpdateLetterAsync(Guid affiliateId, Guid letterId, UpsertReaderLetterRequest r)
    {
        var letter = await _db.ReaderLetters.FirstOrDefaultAsync(l => l.Id == letterId && l.AffiliateId == affiliateId);
        if (letter is null) return null;
        if (letter.Status != "draft") throw new ArgumentException("Esta carta ya salió o está saliendo; no se puede editar.");
        ApplyLetter(letter, r);
        letter.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToLetterDto(letter);
    }

    public async Task<bool> DeleteLetterAsync(Guid affiliateId, Guid letterId)
    {
        var letter = await _db.ReaderLetters.FirstOrDefaultAsync(l => l.Id == letterId && l.AffiliateId == affiliateId);
        if (letter is null) return false;
        if (letter.Status is "queued" or "sending") throw new ArgumentException("La carta está saliendo; espera a que termine.");
        _db.ReaderLetters.Remove(letter);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ReaderLetterDto?> QueueLetterAsync(Guid affiliateId, Guid letterId)
    {
        var letter = await _db.ReaderLetters.FirstOrDefaultAsync(l => l.Id == letterId && l.AffiliateId == affiliateId);
        if (letter is null) return null;
        if (letter.Status != "draft") throw new ArgumentException("Esta carta ya salió o está en cola.");
        if (string.IsNullOrWhiteSpace(letter.BodyHtml)) throw new ArgumentException("La carta está vacía.");

        var recipients = await AudienceQuery(affiliateId, letter.Audience, DateTime.UtcNow).CountAsync();
        if (recipients == 0) throw new ArgumentException("Todavía no hay lectores confirmados para esta carta.");

        letter.Status = "queued";
        letter.QueuedAt = DateTime.UtcNow;
        letter.RecipientCount = recipients;
        letter.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToLetterDto(letter);
    }

    public async Task<bool?> SendTestLetterAsync(Guid affiliateId, Guid letterId, SendTestLetterRequest r)
    {
        var letter = await _db.ReaderLetters.AsNoTracking().FirstOrDefaultAsync(l => l.Id == letterId && l.AffiliateId == affiliateId);
        if (letter is null) return null;
        var author = await _db.Affiliates.AsNoTracking().FirstAsync(a => a.Id == affiliateId);
        var email = string.IsNullOrWhiteSpace(r.Email) ? author.ContactEmail : r.Email;
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Pon un correo para la prueba (o el correo de contacto en Identidad).");
        email = NormalizeEmail(email);
        var test = new ReaderLetterRecipient(Guid.NewGuid(), email, null, author.Language == "en" ? "en" : "es", $"{ReaderMailer.WebUrl}/{author.Slug}");
        var testLetter = new ReaderLetter { Subject = $"[Prueba] {letter.Subject}", BodyHtml = letter.BodyHtml };
        var sent = await _mailer.SendLetterAsync(author, testLetter, new[] { test });
        return sent.Count > 0;
    }

    // ── Worker de cartas ─────────────────────────────────────────────────────────

    public async Task<int> ProcessQueuedLettersAsync(CancellationToken ct)
    {
        var letter = await _db.ReaderLetters
            .Where(l => l.Status == "queued" || l.Status == "sending")
            .OrderBy(l => l.QueuedAt)
            .FirstOrDefaultAsync(ct);
        if (letter is null) return 0;

        var author = await _db.Affiliates.AsNoTracking().FirstOrDefaultAsync(a => a.Id == letter.AffiliateId, ct);
        if (author is null) return 0;

        var now = DateTime.UtcNow;
        var pending = await AudienceQuery(letter.AffiliateId, letter.Audience, now)
            .Where(x => !_db.ReaderLetterDeliveries.Any(d => d.LetterId == letter.Id && d.ReaderId == x.Id))
            .OrderBy(x => x.CreatedAt)
            .Take(LetterBatch)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            letter.Status = "sent";
            letter.SentAt = now;
            letter.SentCount = await _db.ReaderLetterDeliveries.CountAsync(d => d.LetterId == letter.Id && d.SentAt != null, ct);
            letter.FailedCount = await _db.ReaderLetterDeliveries.CountAsync(d => d.LetterId == letter.Id && d.SentAt == null, ct);
            letter.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
            return 0;
        }

        letter.Status = "sending";
        var web = ReaderMailer.WebUrl;
        var recipients = pending.Select(x => new ReaderLetterRecipient(
            x.Id, x.Email, x.Name, x.Language,
            $"{web}/{author.Slug}/lectores/baja?t={Uri.EscapeDataString(_tokens.Unsubscribe(x.Id, author.Id))}")).ToList();

        var sentIds = await _mailer.SendLetterAsync(author, letter, recipients);
        if (sentIds.Count == 0)
        {
            // Nada salió (web caída o sin configurar): no se marca a nadie, se reintenta en el próximo ciclo.
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning("[ReaderClub] carta {Letter}: el lote no salió, se reintenta", letter.Id);
            return 0;
        }

        var sentSet = sentIds.ToHashSet();
        foreach (var x in pending)
        {
            var ok = sentSet.Contains(x.Id);
            _db.ReaderLetterDeliveries.Add(new ReaderLetterDelivery
            {
                Id = Guid.NewGuid(), LetterId = letter.Id, ReaderId = x.Id, CreatedAt = now,
                SentAt = ok ? now : null, Error = ok ? null : "no salió",
            });
        }
        letter.SentCount += sentSet.Count;
        letter.FailedCount += pending.Count - sentSet.Count;
        letter.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return sentSet.Count;
    }

    private IQueryable<Reader> AudienceQuery(Guid affiliateId, string audience, DateTime now)
    {
        var q = _db.Readers.Where(x => x.AffiliateId == affiliateId && x.Status == "confirmed" && x.LettersOptIn);
        if (audience == "members")
            q = q.Where(x => x.MembershipStatus == "active" || (x.MembershipStatus == "past_due" && x.MembershipCurrentPeriodEnd > now));
        return q;
    }

    // ── Apoyo ────────────────────────────────────────────────────────────────────

    private async Task<Affiliate?> AuthorAsync(string slug, bool track = false)
    {
        var q = _db.Affiliates.Where(a => a.Slug == slug && a.Published && AuthorTypes.Contains(a.BusinessType));
        return track ? await q.FirstOrDefaultAsync() : await q.AsNoTracking().FirstOrDefaultAsync();
    }

    private async Task<Reader?> SessionReaderAsync(string slug, string? sessionToken, bool track = true)
    {
        var t = _tokens.Read(sessionToken, "session");
        if (t is null) return null;
        var q = _db.Readers.Where(x => x.Id == t.Value.ReaderId && x.AffiliateId == t.Value.AffiliateId && x.Affiliate!.Slug == slug);
        var reader = track ? await q.FirstOrDefaultAsync() : await q.AsNoTracking().FirstOrDefaultAsync();
        // Darse de baja de todo cierra la sesión, salvo que siga siendo miembro.
        if (reader is null || (reader.Status == "unsubscribed" && !IsMember(reader, DateTime.UtcNow))) return null;
        return reader;
    }

    private async Task<ReaderClubSettings> SettingsAsync(Guid affiliateId, bool track)
    {
        var s = track
            ? await _db.ReaderClubSettings.FirstOrDefaultAsync(x => x.AffiliateId == affiliateId)
            : await _db.ReaderClubSettings.AsNoTracking().FirstOrDefaultAsync(x => x.AffiliateId == affiliateId);
        if (s is not null) return s;
        s = new ReaderClubSettings { Id = Guid.NewGuid(), AffiliateId = affiliateId, CreatedAt = DateTime.UtcNow };
        if (track) _db.ReaderClubSettings.Add(s);
        return s;
    }

    private static bool MembershipAvailable(Affiliate a, ReaderClubSettings s)
        => s.MembershipEnabled && s.MembershipMonthlyPrice is > 0
           && a.StripeConnectChargesEnabled && !string.IsNullOrEmpty(a.StripeConnectAccountId);

    private static RequestOptions Connect(Affiliate a)
    {
        if (string.IsNullOrEmpty(a.StripeConnectAccountId)) throw new ArgumentException("El autor todavía no recibe pagos.");
        return new RequestOptions { StripeAccount = a.StripeConnectAccountId, ApiKey = ApiKey() };
    }

    private static string ApiKey() => Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? string.Empty;

    private async Task SendConfirmAsync(Affiliate author, Reader reader)
    {
        var url = $"{ReaderMailer.WebUrl}/{author.Slug}/lectores/confirmar?t={reader.ConfirmToken:N}";
        await _mailer.SendAccessAsync(author, reader, "confirm", url);
    }

    private async Task SendLoginAsync(Affiliate author, Reader reader)
    {
        var url = $"{ReaderMailer.WebUrl}/{author.Slug}/lectores/entrar?t={Uri.EscapeDataString(_tokens.Login(reader.Id, author.Id))}";
        await _mailer.SendAccessAsync(author, reader, "login", url);
    }

    /// <summary>Menos de un minuto desde el último correo de acceso (alta, reenvío o entrar): no se manda otro.</summary>
    private static bool Cooling(Reader r, DateTime now)
        => (r.UpdatedAt ?? r.CreatedAt) > now - MailCooldown;

    private static ReaderMeDto ToMe(Reader r, DateTime now)
        => new(r.Email, r.Name, r.Status, r.LettersOptIn, IsMember(r, now), r.MembershipStatus, r.MembershipCurrentPeriodEnd);

    private static ReaderClubSettingsDto ToSettingsDto(ReaderClubSettings s, Affiliate a)
        => new(s.LettersEnabled, s.LettersPitch, s.LettersPitchEn, s.MembershipEnabled, s.MembershipMonthlyPrice, s.MembershipCurrency,
               s.MembershipBenefits, s.MembershipBenefitsEn, a.StripeConnectChargesEnabled && !string.IsNullOrEmpty(a.StripeConnectAccountId));

    private static ReaderLetterDto ToLetterDto(ReaderLetter l)
        => new(l.Id, l.Subject, l.BodyHtml, l.Audience, l.Status, l.QueuedAt, l.SentAt, l.RecipientCount, l.SentCount, l.FailedCount, l.CreatedAt, l.UpdatedAt);

    private static void ApplyLetter(ReaderLetter l, UpsertReaderLetterRequest r)
    {
        l.Subject = Clip(r.Subject, 200) ?? throw new ArgumentException("La carta necesita un asunto.");
        l.BodyHtml = HtmlScrub.SanitizeRichText(Clip(r.BodyHtml, 200_000));
        l.Audience = Token(r.Audience, new[] { "all", "members" }, "all");
    }

    private static string NormalizeEmail(string email)
    {
        var e = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (e.Length > 254 || !EmailRx.IsMatch(e)) throw new ArgumentException("Ese correo no parece válido.");
        return e;
    }

    private static string Lang(string? lang) => string.Equals(lang?.Trim(), "en", StringComparison.OrdinalIgnoreCase) ? "en" : "es";

    private static bool IsHttpUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);

    private static string? Clip(string? value, int max)
    {
        var t = value?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length > max ? t[..max] : t;
    }

    private static string Token(string? value, string[] allowed, string fallback)
    {
        var t = value?.Trim().ToLowerInvariant();
        return !string.IsNullOrEmpty(t) && allowed.Contains(t) ? t : fallback;
    }

    private static string Csv(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        // Evita inyección de fórmulas al abrir el CSV en Excel/Sheets.
        var s = v.StartsWith('=') || v.StartsWith('+') || v.StartsWith('-') || v.StartsWith('@') ? "'" + v : v;
        return s.Contains(',') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    }
}
