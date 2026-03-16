using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

public class Service : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public string Category { get; set; } = "General";
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = "Active";

    public Affiliate? Affiliate { get; set; }
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
