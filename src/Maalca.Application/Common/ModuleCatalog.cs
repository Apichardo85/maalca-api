namespace Maalca.Application.Common;

/// <summary>
/// Whitelist of module tokens backed by a real, working endpoint today.
/// Affiliate.Modules is a free-form comma-separated string set at onboarding/admin time —
/// this filters it down so the frontend never renders a module card for data the API can't serve.
/// No legacy token (products, appointments, payments, inventory, queue, team, campaigns) is inferred
/// to mean "catalog"/"page"/"metrics" — that mapping isn't confirmed by the frontend spec, so until
/// onboarding/admin start writing these exact tokens, FilterActive legitimately returns empty.
/// </summary>
public static class ModuleCatalog
{
    public static readonly HashSet<string> Whitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        "catalog", "page", "metrics"
    };

    public static string[] FilterActive(string? modules)
    {
        if (string.IsNullOrWhiteSpace(modules)) return Array.Empty<string>();

        return modules
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(Whitelist.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
