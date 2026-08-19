using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

public class TeamMember : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = "Staff";
    public string Department { get; set; } = "General";
    public DateTime JoinDate { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Foto subida por el dueño desde Equipo. Su sola presencia autoriza mostrarla en
    /// canales públicos (página de reserva, MenuBoard) — sin foto, esos canales caen a un
    /// avatar con iniciales en vez de exponer una imagen no aprobada.</summary>
    public string? PhotoUrl { get; set; }

    public Affiliate? Affiliate { get; set; }
}

public class InventoryItem : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    public string? Category { get; set; }
    public int Quantity { get; set; } = 0;
    public int MinStock { get; set; } = 0;
    public decimal UnitPrice { get; set; }
    public string Status { get; set; } = "Active";
    public string? ImageUrl { get; set; }
    public string? Images { get; set; }
    public bool IsPubliclyVisible { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    public bool IsDemo { get; set; } = false;

    public Affiliate? Affiliate { get; set; }
    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
}

public class InventoryMovement : BaseEntity
{
    public Guid InventoryItemId { get; set; }
    public string Type { get; set; } = "in"; // in, out
    public int Quantity { get; set; }
    public string? Notes { get; set; }

    public InventoryItem? InventoryItem { get; set; }
}

public class QueueEntry : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public Guid? ServiceId { get; set; }
    public Guid? PreferredBarberId { get; set; }
    public string? Notes { get; set; }
    public string Channel { get; set; } = "in-person"; // in-person, phone, web
    public int Position { get; set; }
    public string Status { get; set; } = "waiting"; // waiting, in_service, completed, no_show
    public Guid? AssignedToId { get; set; }
    public DateTime? CalledAt { get; set; }
    // CRM (tarea #244) — resuelto/creado automáticamente por teléfono al entrar a la fila, igual
    // que Appointment. Nullable porque Phone es opcional acá (a diferencia de Appointment) — sin
    // teléfono no hay forma de deduplicar contra un Customer existente.
    public Guid? CustomerId { get; set; }

    public Affiliate? Affiliate { get; set; }
    public Service? Service { get; set; }
    public TeamMember? AssignedTo { get; set; }
    public Customer? Customer { get; set; }
}

public class Product : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; } = 0;
    public string? ImageUrl { get; set; }
    // Galería — JSON array de URLs, orden = orden de visualización. ImageUrl se mantiene
    // sincronizado con Images[0] (o null si Images queda vacío) para que nada que ya lea
    // ImageUrl directamente (templates públicos, MenuBoard, etc.) se entere de este cambio.
    public string? Images { get; set; }
    public string Status { get; set; } = "Active";
    public bool IsPubliclyVisible { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    public bool IsDemo { get; set; } = false;

    // ── Menu-style fields (migrated from the legacy Supabase `dishes` table) ──
    // Periods/WeekDays/Flags are comma-separated token lists, same convention as
    // Affiliate.ModulosActivos — not a persisted enum, so the wire tokens stay
    // snake_case-compatible with the legacy data (breakfast, late_night, ...).
    public string? DescriptionEn { get; set; }
    public string? Periods { get; set; }
    public string? WeekDays { get; set; }
    public string? Flags { get; set; }
    public bool Featured { get; set; } = false;
    public bool Popular { get; set; } = false;

    // Fase 9 Etapa A (Pantallas — Menu Board con video): solo en Product, no en
    // Service/InventoryItem — el caso real es "clip corto de un platillo" para un
    // restaurante, no una foto de servicio o de inventario. Si hace falta ampliar a los
    // otros tipos más adelante, se agrega igual (campo nullable, no rompe nada existente).
    public string? VideoUrl { get; set; }

    public Affiliate? Affiliate { get; set; }
}

public class Invoice : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public Guid CustomerId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Paid, Overdue, Cancelled
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? Notes { get; set; }

    public Affiliate? Affiliate { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}

public class InvoiceItem : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }

    public Invoice? Invoice { get; set; }
}

public class AgentExecution : BaseEntity
{
    public int IssueNumber { get; set; }
    public string IssueTitle { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string AgentRole { get; set; } = string.Empty; // frontend, backend, architect, qa
    public string ModelUsed { get; set; } = string.Empty; // groq/llama-3.3-70b, openrouter/llama-3.3-70b
    public string Tier { get; set; } = "free"; // free, standard, premium
    public int TokensInput { get; set; }
    public int TokensOutput { get; set; }
    public decimal CostUsd { get; set; }
    public long DurationMs { get; set; }
    public string Status { get; set; } = "running"; // running, success, failed, timeout
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? PrUrl { get; set; }
    public string? BranchName { get; set; }
}

public class AffiliateMilestone : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public string Key { get; set; } = null!;       // MilestoneKeys constant
    public string? Metadata { get; set; }           // JSON, optional context

    public Affiliate Affiliate { get; set; } = null!;
}

public class Lead : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string Source { get; set; } = string.Empty; // properties, cirisonic
    public string? PropertyId { get; set; }
    public string? ProjectType { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; } = "New"; // New, Contacted, Qualified, Converted, Lost
}
