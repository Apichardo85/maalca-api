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

    public Affiliate? Affiliate { get; set; }
}

public class InventoryItem : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "General";
    public int Quantity { get; set; } = 0;
    public int MinStock { get; set; } = 0;
    public decimal UnitPrice { get; set; }
    public string Status { get; set; } = "Active";

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

    public Affiliate? Affiliate { get; set; }
    public Service? Service { get; set; }
    public TeamMember? AssignedTo { get; set; }
}

public class Product : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "General";
    public decimal Price { get; set; }
    public int Stock { get; set; } = 0;
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = "Active";

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

public class GiftCard : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal InitialAmount { get; set; }
    public decimal Balance { get; set; }
    public string? RecipientEmail { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; } = "Active"; // Active, Redeemed, Expired
    public DateTime? ExpiresAt { get; set; }

    public Affiliate? Affiliate { get; set; }
}

public class Campaign : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "email"; // email, sms, push
    public string? TargetAudience { get; set; }
    public string? Content { get; set; }
    public DateTime? Schedule { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, Scheduled, Sent, Failed

    public Affiliate? Affiliate { get; set; }
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
