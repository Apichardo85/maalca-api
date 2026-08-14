namespace Maalca.Application.Common;

/// <summary>
/// Whitelist of module tokens backed by a real, working endpoint today.
/// Reads from Affiliate.ModulosActivos (not the legacy Affiliate.Modules field, which still
/// drives the old /dashboard/[affiliateId] UI and must not be repurposed). This filters it down
/// so the frontend never renders a module card for data the API can't serve. New code (onboarding,
/// future admin panel) must write these exact canonical tokens directly — no legacy→canonical
/// translation layer.
/// </summary>
public static class ModuleCatalog
{
    public static readonly HashSet<string> Whitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        "catalog", "page", "metrics", "staff"
        // "appointments" se agrega cuando la Agenda esté conectada (fase siguiente) — hasta
        // entonces no debe aparecer como módulo "activo" aunque el endpoint ya exista.
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
