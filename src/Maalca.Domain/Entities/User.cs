using Maalca.Domain.Common;
using Maalca.Domain.Enums;

namespace Maalca.Domain.Entities;

public class User : AuditableEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Guid? AffiliateId { get; set; }
    public string Role { get; set; } = "User"; // Admin, Manager, User
    public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    public Affiliate? Affiliate { get; set; }
}

public class Affiliate : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    public string? Logo { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? HeroImage { get; set; }
    public string Modules { get; set; } = ""; // Legacy comma-separated list — still read by GET /api/affiliates/{id} for the pre-Espacio-v2 dashboard. Do not repurpose or overwrite.
    public string Features { get; set; } = "{}"; // JSON string
    public string Settings { get; set; } = "{}"; // JSON string
    public bool IsActive { get; set; } = true;

    // ── Fase B/Paso 2: canonical whitelist tokens for the Espacio v2 dashboard (catalog/page/metrics).
    // Coexists with the legacy Modules field above instead of replacing it, since Modules still
    // drives the old /dashboard/[affiliateId] UI. New code (onboarding, future admin panel) must
    // write canonical tokens directly here — no legacy→canonical translation layer.
    public string? ModulosActivos { get; set; }

    // ── Espacio v2: contenido editorial de la página pública ──────
    // JSON string, sin shape forzado a nivel de DB. ProcessSteps: array de
    // {title, description}. Faq: array de {question, answer}. Horario: array de
    // {dia, abre, cierra, cerrado}, uno por día de la semana. Parseados a DTOs
    // tipados en el borde (ver JsonArrayField) — nunca expuestos como string crudo.
    public string? ProcessSteps { get; set; }
    public string? Faq { get; set; }
    public string? Horario { get; set; }

    // IANA timezone id (e.g. "America/New_York"), set explicitly per affiliate — never inferred.
    public string? Timezone { get; set; }

    // ── Fase B: SaaS public fields ──────────────────────────────
    public string? Slug { get; set; }
    public BusinessType BusinessType { get; set; } = BusinessType.Service;
    public Plan Plan { get; set; } = Plan.Free;
    public PlanStatus PlanStatus { get; set; } = PlanStatus.Active;
    public bool Published { get; set; } = false;

    // Curación manual (por SQL) de qué afiliados aparecen en la home de MaalCa.
    public bool IsFeatured { get; set; } = false;
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public DateTime? PlanStartedAt { get; set; }

    // ── Stripe Connect: destino de pago del afiliado (distinto de StripeCustomerId,
    // que es al afiliado como CLIENTE de MaalCa). Esta cuenta conectada es donde
    // el afiliado recibe el dinero de SUS PROPIOS clientes. Cuenta tipo Standard,
    // charges directos — ver StripeConnectService. Los tres booleanos son un cache
    // local de las capabilities de la cuenta en Stripe; se refrescan por webhook
    // (account.updated) y por consulta explícita, nunca se infieren localmente.
    public string? StripeConnectAccountId { get; set; }
    public bool StripeConnectChargesEnabled { get; set; } = false;
    public bool StripeConnectPayoutsEnabled { get; set; } = false;
    public bool StripeConnectDetailsSubmitted { get; set; } = false;
    public DateTime? StripeConnectUpdatedAt { get; set; }

    // ── Fase B: Branding (Description, PrimaryColor, Logo, HeroImage preexisten) ──
    public string? LogoUrl { get; set; }
    public string? CoverImageUrl { get; set; }

    // ── Fase B: Contact ──────────────────────────────────────
    public string? WhatsApp { get; set; }
    public string? ContactEmail { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }

    // ISO 3166-1 alpha-2 (e.g. "US", "DO"). Usado por StripeConnectService al crear la cuenta
    // conectada del afiliado — Stripe la exige y no se puede cambiar después de creada. Null
    // hasta que el afiliado la configure; StripeConnectService cae a "US" solo como último
    // recurso si sigue sin setearse.
    public string? Country { get; set; }

    // Fase 9 Etapa A — cada cuántos slides de menú se inserta un comercial en el Menu Board
    // público. Null/0 = sin comerciales intercalados (comportamiento actual, sin cambios para
    // quien no configure esto). Simple a propósito — nada de listas de orden explícito todavía.
    public int? AdFrequency { get; set; }

    // Fase 9 — preferencia del NEGOCIO, no del visitante. El Menu Board no tiene usuario que le
    // dé click a un toggle de idioma (nadie interactúa con una TV) — a diferencia del resto del
    // sitio público, que usa la preferencia de cada visitante (useSimpleLanguage). "es" es el
    // default porque es el idioma con el que se creó todo el catálogo existente hasta ahora.
    public string Language { get; set; } = "es";
    public BoardTheme BoardTheme { get; set; } = BoardTheme.Dark;
    public BoardTransitionEffect TransitionEffect { get; set; } = BoardTransitionEffect.Fade;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<Service> Services { get; set; } = new List<Service>();
    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
    public ICollection<QueueEntry> QueueEntries { get; set; } = new List<QueueEntry>();
    public ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<GiftCard> GiftCards { get; set; } = new List<GiftCard>();
    public ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();
}
