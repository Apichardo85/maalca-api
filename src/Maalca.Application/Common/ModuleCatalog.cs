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
        "catalog", "page", "metrics", "staff", "appointments"
    };

    public static string[] FilterActive(string? modules)
    {
        // ModulosActivos nunca se ha escrito para ningún afiliado (ni onboarding ni ningún
        // servicio lo setea todavía) — hasta que exista un admin panel real para prender/apagar
        // módulos por afiliado, "sin configurar" significa "todos los módulos base activos",
        // que es el comportamiento real de hoy (catalog/page/metrics/staff/appointments
        // funcionan para cualquier afiliado sin excepción). El día que se construya el toggle
        // real, un afiliado con ModulosActivos="" explícito (no null) podría usarse para "ninguno".
        if (string.IsNullOrWhiteSpace(modules)) return Whitelist.ToArray();

        return modules
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(Whitelist.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
