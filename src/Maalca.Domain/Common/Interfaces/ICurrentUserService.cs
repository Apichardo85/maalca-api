namespace Maalca.Domain.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? AffiliateId { get; }
    string? Email { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
}
