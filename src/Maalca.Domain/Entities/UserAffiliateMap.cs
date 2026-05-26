using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

public class UserAffiliateMap : BaseEntity
{
    public string SupabaseUserId { get; set; } = null!;
    public string Email { get; set; } = null!;
    public Guid AffiliateId { get; set; }
    public Affiliate Affiliate { get; set; } = null!;
    public AffiliateRole Role { get; set; }
}

public enum AffiliateRole { Owner = 0, Manager = 1, Staff = 2 }
