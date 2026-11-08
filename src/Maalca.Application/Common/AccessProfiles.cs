namespace Maalca.Application.Common;

/// <summary>
/// Perfiles de acceso del equipo (chofer, cajero, almacén, contabilidad). El rol de dashboard (Dueño/Gerente/Empleado)
/// dice cuánto puede ADMINISTRAR; el perfil dice QUÉ PARTES del sistema puede usar. Un usuario con perfil solo llega a los
/// endpoints de la lista de su perfil; todo lo demás responde 403, aunque escriba la dirección a mano. El Dueño nunca
/// tiene perfil. Un Empleado sin perfil conserva el comportamiento anterior (las restricciones de cada endpoint).
/// La web (maalca-web/src/lib/access-profiles.ts) espeja esta lista para esconder el menú: mantener ambas iguales.
/// </summary>
public static class AccessProfiles
{
    public const string Driver = "driver";
    public const string Cashier = "cashier";
    public const string Warehouse = "warehouse";
    public const string Accounting = "accounting";

    public static readonly IReadOnlyList<string> All = new[] { Driver, Cashier, Warehouse, Accounting };

    private enum Level { Read = 1, ReadCreate = 2, Full = 3 }

    // Segmento = lo que sigue a /api/affiliates/{id}/ en la ruta.
    private static readonly Dictionary<string, Level> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        ["notifications"] = Level.Full,
        ["push-subscriptions"] = Level.Full,
        ["tasks"] = Level.Full,
        ["modules"] = Level.Read,
    };

    private static readonly Dictionary<string, Dictionary<string, Level>> Rules = new(StringComparer.OrdinalIgnoreCase)
    {
        // Conductor: sus rutas (el filtro por conductor ya lo hace DriverScope), escanear anaqueles y ver productos.
        [Driver] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["routes"] = Level.Full,
            ["anaqueles"] = Level.Full,
            ["catalog-items"] = Level.Read,
        },
        // Cajero: punto de venta y pedidos, clientes (ver/crear), facturas y cobros (registrar abonos; contratos solo lectura).
        [Cashier] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["orders"] = Level.Full,
            ["invoices"] = Level.Full,
            ["payments"] = Level.ReadCreate,
            ["receivables"] = Level.Read,
            ["receivables-settings"] = Level.Read,
            ["contracts"] = Level.Read,
            ["contract-template"] = Level.Read,
            ["customers"] = Level.ReadCreate,
            ["catalog-items"] = Level.Read,
            ["products"] = Level.Read,
            ["inventory"] = Level.Read,
        },
        // Almacén: inventario y consulta de envases.
        [Warehouse] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["inventory"] = Level.Full,
            ["catalog-items"] = Level.Read,
            ["products"] = Level.Read,
            ["container-types"] = Level.Read,
            ["container-movements"] = Level.Read,
            ["anaqueles"] = Level.Read,
        },
        // Contabilidad: facturas, contratos y cobros, gastos y reportes. Sin equipo, nómina, ajustes ni facturación de MaalCa.
        [Accounting] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["invoices"] = Level.Full,
            ["contracts"] = Level.Full,
            ["contract-template"] = Level.Read,
            ["payments"] = Level.Full,
            ["receivables"] = Level.Full,
            ["receivables-settings"] = Level.Read,
            ["expenses"] = Level.Full,
            ["reports"] = Level.Read,
            ["customers-report"] = Level.Read,
            ["customers"] = Level.ReadCreate,
            ["catalog-items"] = Level.Read,
            ["price-lists"] = Level.Read,
            ["metrics"] = Level.Read,
            ["board"] = Level.Read,
            ["routes"] = Level.Read,
        },
    };

    /// <summary>Normaliza lo que llega del cliente: vacío = sin perfil.</summary>
    public static string? Normalize(string? profile)
    {
        var v = profile?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    public static bool IsKnown(string? profile) => profile is not null && All.Contains(profile);

    /// <summary>¿Puede este perfil llamar a /api/affiliates/{id}/{segment} con este método HTTP?</summary>
    public static bool IsAllowed(string profile, string segment, string httpMethod)
    {
        if (!Rules.TryGetValue(profile, out var own)) return false; // perfil desconocido: nada
        if (!own.TryGetValue(segment, out var level) && !Common.TryGetValue(segment, out level)) return false;

        var read = string.Equals(httpMethod, "GET", StringComparison.OrdinalIgnoreCase)
                || string.Equals(httpMethod, "HEAD", StringComparison.OrdinalIgnoreCase)
                || string.Equals(httpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase);
        return level switch
        {
            Level.Full => true,
            Level.ReadCreate => read || string.Equals(httpMethod, "POST", StringComparison.OrdinalIgnoreCase),
            _ => read,
        };
    }
}
