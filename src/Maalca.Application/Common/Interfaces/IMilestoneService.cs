namespace Maalca.Application.Common.Interfaces;

public static class MilestoneKeys
{
    public const string FirstProductAdded = "first_product_added";
    public const string WhatsAppConfigured = "whats_app_configured";
    public const string LinkShared = "link_shared";
}

public interface IMilestoneService
{
    Task<HashSet<string>> GetCompletedKeysAsync(Guid affiliateId);
    Task MarkAsync(Guid affiliateId, string key, string? metadata = null);
}
