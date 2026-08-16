namespace Maalca.Application.Common;

/// <summary>
/// Whitelist of module tokens backed by a real, working endpoint today — matches the nav items
/// SpaceSidebar/SpaceMobileNav actually render (minus Dashboard/Diseñar/Identidad/Módulos,
/// que siempre están visibles porque son la casa del afiliado, no un módulo apagable).
/// Reads from Affiliate.ModulosActivos (not the legacy Affiliate.Modules field, which still
/// drives the old /dashboard/[affiliateId] UI and must not be repurposed). Toggle real desde
/// /ops (Etapa: control de módulos por afiliado, ver PlatformAdminService.SetAffiliateModulesAsync)
/// — MaalCa puede prender/apagar cualquiera de estos por afiliado, incluso por encima de lo que
/// el plan normalmente incluiría (no hay gating de plan sobre estos tokens, es autoridad de
/// plataforma pura).
/// </summary>
public static class ModuleCatalog
{
    public static readonly HashSet<string> Whitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        "catalog", "page", "metrics", "staff", "appointments",
        "orders", "kitchen", "pos", "board", "billing",
        "invoices", "queue", "reservations",
    };

    public static string[] FilterActive(string? modules)
    {
        // null = nunca configurado por un admin — compat con todos los afiliados existentes
        // (creados antes de que este toggle existiera): "sin configurar" sigue significando
        // "todos los módulos activos", que es el comportamiento real de siempre. Una vez un
        // admin guarda algo explícito desde /ops (aunque sea la lista completa, o vacía a
        // propósito para desactivar todo), esa cadena — incluida "" — es la fuente de verdad y
        // ya NO cae al default de "todos".
        if (modules is null) return Whitelist.ToArray();
        if (modules.Trim().Length == 0) return Array.Empty<string>();

        return modules
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(Whitelist.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
