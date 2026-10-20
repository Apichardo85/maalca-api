using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

public class Customer : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Active"; // Active, Inactive
    public DateTime? LastVisit { get; set; }
    public int TotalVisits { get; set; } = 0;
    /// Pedidos para recoger que nunca vino a buscar (marcados "No vino" en la Estación/Pedidos).
    public int NoShows { get; set; } = 0;

    public Affiliate? Affiliate { get; set; }
}
