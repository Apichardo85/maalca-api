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
        "invoices", "queue", "reservations", "proposals",
    };

    // Espejo de businessTypes/excludeBusinessTypes en maalca-web/src/lib/module-catalog.ts --
    // solo se usa para calcular el default "nunca configurado" (ver FilterActive). Antes ese
    // default era el Whitelist completo sin importar el tipo de negocio, y hasta ahora no
    // importaba porque el frontend (SpaceSidebar/page.tsx de cada modulo) tenia su propio
    // filtro por businessType encima. Al quitar ese filtro duplicado (fix de gates reales por
    // modulo) quedo expuesto: cualquier afiliado nunca tocado desde /ops empezo a ver Cocina/
    // Fila/Facturas/Reservas/Propuestas sin importar su rubro. Este mapa reproduce el mismo
    // default sensato del lado del servidor, sin tocar el comportamiento real de /ops (activar
    // un token explicito para un tipo atipico sigue funcionando igual, por encima del default).
    private static readonly Dictionary<string, string[]> DefaultBusinessTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kitchen"] = new[] { "restaurant" },
        ["pos"] = new[] { "restaurant", "retail" },
        ["queue"] = new[] { "barber" },
        ["invoices"] = new[] { "service", "professional" },
        ["reservations"] = new[] { "restaurant" },
        ["proposals"] = new[] { "service", "professional" },
    };
    private static readonly string[] AppointmentsExcludedBusinessTypes = { "retail", "creator", "publisher", "restaurant" };

    public static string[] FilterActive(string? modules, string? businessType = null)
    {
        // null = nunca configurado por un admin — compat con todos los afiliados existentes
        // (creados antes de que este toggle existiera): "sin configurar" sigue significando
        // "los módulos que le corresponden por tipo de negocio" (ver DefaultBusinessTypes), no
        // literalmente todos. Una vez un admin guarda algo explícito desde /ops (aunque sea la
        // lista completa, o vacía a propósito para desactivar todo), esa cadena — incluida "" —
        // es la fuente de verdad y ya NO cae a este default.
        if (modules is null) return DefaultForBusinessType(businessType);
        if (modules.Trim().Length == 0) return Array.Empty<string>();

        return modules
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(Whitelist.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] DefaultForBusinessType(string? businessType)
    {
        var bt = businessType?.ToLowerInvariant() ?? "";
        return Whitelist.Where(token => IsRelevantByDefault(token, bt)).ToArray();
    }

    private static bool IsRelevantByDefault(string token, string businessType)
    {
        if (token.Equals("appointments", StringComparison.OrdinalIgnoreCase))
            return !AppointmentsExcludedBusinessTypes.Contains(businessType);
        if (DefaultBusinessTypes.TryGetValue(token, out var allowed))
            return allowed.Contains(businessType, StringComparer.OrdinalIgnoreCase);
        return true;
    }
}
