using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Common;

/// <summary>
/// Resuelve la lista de precios de un cliente con sesión en una tienda — lo usan el pedido (OrderService) y los
/// precios de miembro (StoreAccountService), así lo que el cliente ve es lo que se cobra. Solo cuenta una cuenta
/// APROBADA, con lista ACTIVA, en una tienda con el módulo "pricelists" activo; cualquier otra cosa = precio público.
/// </summary>
public static class StoreAccounts
{
    public const string ModuleToken = "pricelists";
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";

    public sealed record ResolvedAccount(Guid StoreCustomerId, string Status, PriceListContext? PriceList);

    public static async Task<ResolvedAccount?> ResolveAsync(AppDbContext db, Guid affiliateId, string supabaseUserId)
    {
        if (string.IsNullOrWhiteSpace(supabaseUserId)) return null;
        var account = await db.StoreCustomers.AsNoTracking()
            .Where(c => c.AffiliateId == affiliateId && c.SupabaseUserId == supabaseUserId)
            .Select(c => new { c.Id, c.Status, c.PriceListId })
            .FirstOrDefaultAsync();
        if (account is null) return null;
        var list = account.Status == Approved && account.PriceListId is { } listId
            ? await PriceListPricing.LoadAsync(db, affiliateId, listId)
            : null;
        return new ResolvedAccount(account.Id, account.Status, list);
    }
}
