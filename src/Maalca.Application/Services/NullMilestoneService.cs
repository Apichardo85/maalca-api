using Maalca.Application.Common.Interfaces;

namespace Maalca.Application.Services;

// Stub until AffiliateMilestones table is created (addendum Correction #2).
// All progress flags return false; MarkAsync is a no-op.
public sealed class NullMilestoneService : IMilestoneService
{
    public Task<HashSet<string>> GetCompletedKeysAsync(Guid affiliateId)
        => Task.FromResult(new HashSet<string>());

    public Task MarkAsync(Guid affiliateId, string key, string? metadata = null)
        => Task.CompletedTask;
}
