using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Club de lectores de un autor (BusinessType.Creator): quien deja su correo para recibir las cartas,
/// y opcionalmente paga una membresía mensual que abre las obras en ReadingAccess "members".
///
/// Identidad del lector = (AffiliateId, Email). Sin contraseña: entra con un enlace firmado que llega
/// a su correo (ReaderTokens en Application). Doble opt-in: queda "pending" hasta que confirma.
/// La membresía se cobra en la cuenta Stripe Connect del autor (cargo directo, igual que pedidos y
/// donaciones); Stripe es la fuente de verdad y el webhook mantiene MembershipStatus al día.
/// </summary>
public class Reader : BaseEntity
{
    public Guid AffiliateId { get; set; }
    /// <summary>Siempre en minúscula y sin espacios: la unicidad (AffiliateId, Email) depende de eso.</summary>
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    /// <summary>"es" | "en" — idioma de las cartas y de los correos de acceso.</summary>
    public string Language { get; set; } = "es";

    /// <summary>"pending" (no confirmó) | "confirmed" | "unsubscribed" (se dio de baja de todo).</summary>
    public string Status { get; set; } = "pending";
    /// <summary>Recibe las cartas. Darse de baja de las cartas no cancela la membresía.</summary>
    public bool LettersOptIn { get; set; } = true;
    public Guid ConfirmToken { get; set; } = Guid.NewGuid();
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? UnsubscribedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Dónde se apuntó: "page" | "work" | "reader" | "checkout" | "import".</summary>
    public string Source { get; set; } = "page";
    /// <summary>Obra desde la que se apuntó (llave, no título). Se pone en null si la obra se borra.</summary>
    public Guid? SourceWorkId { get; set; }

    // ── Membresía (cuenta Stripe Connect del autor) ──
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    /// <summary>"none" | "active" | "past_due" | "canceled". Solo "active" (y "past_due" dentro del período) abre lo de miembros.</summary>
    public string MembershipStatus { get; set; } = "none";
    public DateTime? MembershipCurrentPeriodEnd { get; set; }
    public DateTime? MembershipStartedAt { get; set; }

    public Affiliate? Affiliate { get; set; }
}

/// <summary>Ajustes del club por autor (una fila por afiliado). Se editan en /space/[slug]/readers.</summary>
public class ReaderClubSettings : BaseEntity
{
    public Guid AffiliateId { get; set; }
    /// <summary>Muestra el formulario de las cartas en la página. Apagado = no se aceptan suscripciones nuevas.</summary>
    public bool LettersEnabled { get; set; } = true;
    /// <summary>Qué recibe quien se suscribe (texto del autor, se muestra junto al formulario).</summary>
    public string? LettersPitch { get; set; }
    public string? LettersPitchEn { get; set; }

    public bool MembershipEnabled { get; set; } = false;
    public decimal? MembershipMonthlyPrice { get; set; }
    /// <summary>"USD" | "DOP".</summary>
    public string MembershipCurrency { get; set; } = "USD";
    public string? MembershipBenefits { get; set; }
    public string? MembershipBenefitsEn { get; set; }
    /// <summary>Producto y precio en la cuenta Connect del autor. El precio se vuelve a crear si cambia el monto o la moneda.</summary>
    public string? StripeProductId { get; set; }
    public string? StripePriceId { get; set; }
    public decimal? StripePriceAmount { get; set; }
    public string? StripePriceCurrency { get; set; }

    public Affiliate? Affiliate { get; set; }
}

/// <summary>Una carta (newsletter) del autor a sus lectores.</summary>
public class ReaderLetter : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public string Subject { get; set; } = string.Empty;
    /// <summary>HTML saneado con HtmlScrub al guardar.</summary>
    public string? BodyHtml { get; set; }
    /// <summary>"all" (todos los confirmados con cartas activas) | "members" (solo miembros activos).</summary>
    public string Audience { get; set; } = "all";
    /// <summary>"draft" | "queued" | "sending" | "sent". Solo un borrador se edita.</summary>
    public string Status { get; set; } = "draft";
    public DateTime? QueuedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public int RecipientCount { get; set; } = 0;
    public int SentCount { get; set; } = 0;
    public int FailedCount { get; set; } = 0;

    public Affiliate? Affiliate { get; set; }
}

/// <summary>Una entrega de una carta a un lector. Única por (LetterId, ReaderId): el envío es idempotente y se puede reanudar.</summary>
public class ReaderLetterDelivery : BaseEntity
{
    public Guid LetterId { get; set; }
    public Guid ReaderId { get; set; }
    public DateTime? SentAt { get; set; }
    public string? Error { get; set; }

    public ReaderLetter? Letter { get; set; }
    public Reader? Reader { get; set; }
}

/// <summary>Último capítulo abierto por un lector en una obra ("seguir leyendo"). Único por (ReaderId, WorkId).</summary>
public class ReaderProgress : BaseEntity
{
    public Guid ReaderId { get; set; }
    public Guid WorkId { get; set; }
    public int ChapterNumber { get; set; }

    public Reader? Reader { get; set; }
    public Work? Work { get; set; }
}
