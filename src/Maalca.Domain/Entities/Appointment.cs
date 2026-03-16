using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

public class Appointment : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ServiceId { get; set; }
    public DateTime Date { get; set; }
    public string Time { get; set; } = string.Empty; // HH:mm format
    public string Status { get; set; } = "Scheduled"; // Scheduled, Confirmed, InProgress, Completed, Cancelled, NoShow
    public string? Notes { get; set; }
    public Guid? AssignedToId { get; set; } // Team member

    public Affiliate? Affiliate { get; set; }
    public Customer? Customer { get; set; }
    public Service? Service { get; set; }
    public TeamMember? AssignedTo { get; set; }
}
