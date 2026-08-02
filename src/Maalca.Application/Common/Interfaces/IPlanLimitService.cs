using Maalca.Domain.Entities;
using Maalca.Domain.Enums;

namespace Maalca.Application.Common.Interfaces;

public interface IPlanLimitService
{
    Task<bool> CanAddItemAsync(Guid affiliateId);
    Task<int> GetCurrentItemCountAsync(Guid affiliateId);
    int GetMaxItems(Plan plan);
    bool IsTrialExpired(Affiliate affiliate);
}
