using Maalca.Domain.Entities;
using Maalca.Domain.Enums;

namespace Maalca.Application.Common.Interfaces;

public interface IPlanLimitService
{
    Task<bool> CanAddItemAsync(Guid affiliateId);
    Task<int> GetCurrentItemCountAsync(Guid affiliateId);
    int GetMaxItems(Plan plan);
    int GetMaxItems(Affiliate affiliate);
    bool IsTrialExpired(Affiliate affiliate);
    int GetIncludedScreens(Plan plan);
    int GetScreenAllowance(Affiliate affiliate);
    /// <summary>Lanza ScreenLimitException si agregar <paramref name="adding"/> pantallas supera el plan.</summary>
    Task EnsureScreenCapacityAsync(Guid affiliateId, int adding);
}
