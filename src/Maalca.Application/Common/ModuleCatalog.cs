using Maalca.Domain.Enums;

namespace Maalca.Application.Common;

/// <summary>Un módulo activable del espacio del afiliado.</summary>
public record ModuleDefinition(
    string Token,
    string Es,
    string En,
    string DescEs,
    string DescEn,
    string[]? BusinessTypes,
    string[]? ExcludeBusinessTypes,
    PlanTier DefaultMinTier);

/// <summary>
/// Catálogo único de módulos — fuente de verdad. La web lo lee de GET /api/modules/catalog (con
/// el plan mínimo vigente de ModulePlanRules); antes vivía duplicado en maalca-web/src/lib/
/// module-catalog.ts y aquí, sincronizado a mano, y así se quedó la Estación fuera.
///
/// Facturación (el plan de MaalCa del negocio) NO es un módulo: siempre visible, nunca se apaga.
/// Dashboard, Diseñar, Identidad, Módulos y Clientes tampoco: son la casa del afiliado.
///
/// Qué módulos ve un negocio lo decide ModuleAccess (tipo de negocio ∩ lo prendido en ops ∩ el
/// plan), no esta clase.
/// </summary>
public static class ModuleCatalog
{
    public static readonly IReadOnlyList<ModuleDefinition> Modules = new[]
    {
        new ModuleDefinition("catalog", "Catálogo", "Catalog",
            "Tus items y precios, siempre al día.", "Your items and prices, always up to date.",
            null, new[] { "dealer" }, PlanTier.Starter),
        // Dealer: el inventario de vehículos reemplaza al catálogo (VehicleListing, ver plans/spec-espacio-dealer.md).
        new ModuleDefinition("vehicles", "Vehículos", "Vehicles",
            "Tu inventario de vehículos y los interesados en cada uno.", "Your vehicle inventory and the leads on each one.",
            new[] { "dealer" }, null, PlanTier.Starter),
        new ModuleDefinition("page", "Página", "Page",
            "Tu página pública en maalca.com.", "Your public page on maalca.com.",
            null, null, PlanTier.Starter),
        new ModuleDefinition("metrics", "Estadísticas", "Stats",
            "Visitas y actividad de tu página.", "Visits and activity on your page.",
            null, null, PlanTier.Starter),
        new ModuleDefinition("board", "Pantalla", "Screen",
            "Menú, anuncios o contenido en las TVs de tu local.", "Menu, ads or content on the TVs in your place.",
            null, new[] { "dealer" }, PlanTier.Growth),
        new ModuleDefinition("orders", "Pedidos", "Orders",
            "Pedidos online: para recoger, en mesa o con tarjeta.", "Online orders: pickup, at the table or by card.",
            null, new[] { "dealer" }, PlanTier.Growth),
        new ModuleDefinition("kitchen", "Cocina", "Kitchen",
            "Kanban en tiempo real para preparar pedidos.", "Real-time kanban to prepare orders.",
            new[] { "restaurant" }, null, PlanTier.Growth),
        new ModuleDefinition("station", "Estación", "Station",
            "Pantalla de pedidos que suena hasta que alguien acepta.", "Order screen that rings until someone accepts.",
            new[] { "restaurant" }, null, PlanTier.Growth),
        new ModuleDefinition("pos", "Punto de venta", "Point of sale",
            "Cobra en el local — efectivo, tarjeta o QR.", "Charge in-store — cash, card, or QR.",
            new[] { "restaurant", "retail" }, null, PlanTier.Growth),
        new ModuleDefinition("modifiers", "Guarniciones", "Modifiers",
            "Grupos reusables de acompañantes/extras con su precio real.", "Reusable groups of sides/extras with their real price.",
            new[] { "restaurant" }, null, PlanTier.Growth),
        new ModuleDefinition("shipping", "Envíos", "Shipping",
            "Envío a domicilio con tus tarifas: costo fijo, gratis desde un monto y número de rastreo.", "Home delivery with your own rates: flat cost, free over an amount, and tracking number.",
            new[] { "retail" }, null, PlanTier.Growth),
        new ModuleDefinition("pricelists", "Listas de precio", "Price lists",
            "Precios para miembros y mayoristas: descuento general o precio fijo por producto.", "Member and wholesale pricing: overall discount or fixed price per product.",
            new[] { "retail" }, null, PlanTier.Professional),
        new ModuleDefinition("inventory", "Inventario", "Inventory",
            "Stock de insumos con alerta de mínimo y registro de entradas/salidas.", "Supply stock with low-stock alerts and in/out tracking.",
            new[] { "restaurant", "retail", "community" }, null, PlanTier.Growth),
        new ModuleDefinition("reservations", "Reservas", "Reservations",
            "Reserva de mesa — cuántas personas y a qué hora.", "Table reservations — party size and time.",
            new[] { "restaurant" }, null, PlanTier.Growth),
        new ModuleDefinition("appointments", "Agenda", "Agenda",
            "Citas agendadas, asignadas a tu personal.", "Booked appointments, assigned to your staff.",
            null, new[] { "retail", "creator", "publisher", "restaurant", "community", "dealer" }, PlanTier.Growth),
        new ModuleDefinition("queue", "Fila de espera", "Waiting queue",
            "Walk-ins que esperan turno sin cita previa.", "Walk-ins waiting their turn without an appointment.",
            new[] { "barber" }, null, PlanTier.Growth),
        new ModuleDefinition("staff", "Equipo", "Team",
            "Tu equipo de trabajo — meseros, barberos, etc.", "Your operating staff — waiters, barbers, etc.",
            null, null, PlanTier.Growth),
        new ModuleDefinition("workforce", "Ponche y nómina", "Time clock & payroll",
            "Reloj de entrada/salida por PIN, reporte de nómina y tareas asignadas.", "PIN clock-in/out, payroll report, and assigned tasks.",
            null, null, PlanTier.Professional),
        new ModuleDefinition("invoices", "Facturas", "Invoices",
            "Factura a tus clientes por el trabajo realizado.", "Invoice your customers for completed work.",
            null, new[] { "community" }, PlanTier.Professional),
        // Facturación electrónica (e-CF, DGII). Opt-in: el negocio la prende y completa su configuración fiscal.
        new ModuleDefinition("fiscal", "Factura electrónica", "E-invoicing",
            "Comprobantes fiscales electrónicos (e-CF) de la DGII desde tus ventas.", "Electronic tax receipts (e-CF) for the Dominican DGII from your sales.",
            new[] { "restaurant", "retail", "dealer" }, null, PlanTier.Professional),
        new ModuleDefinition("proposals", "Propuestas", "Proposals",
            "Envía cotizaciones y deja que el cliente firme y acepte en línea.", "Send quotes and let clients sign and accept online.",
            null, new[] { "community" }, PlanTier.Professional),
        // Clientes por contrato (Nueva Era / agua y hielo, Fase 1): contratos, abonos, estado de cuenta y antigüedad
        // de saldos. Solo retail y plan Professional a propósito: FilterActive prende por defecto todo módulo relevante
        // para negocios nunca configurados, y este no debe aparecer de golpe en tiendas existentes.
        new ModuleDefinition("contracts", "Contratos y cobros", "Contracts & collections",
            "Contratos por cliente, abonos, estado de cuenta y antigüedad de saldos.", "Customer contracts, payments, statements and aging.",
            new[] { "retail" }, null, PlanTier.Professional),

        // Reparto: rutas del día, entregas con envases y cobro en ruta (PWA del conductor). Mismo criterio que contracts.
        new ModuleDefinition("routes", "Rutas y reparto", "Routes & delivery",
            "Rutas del día, entregas con envases, cobro en ruta y cuadre de cierre.", "Daily routes, deliveries with containers, on-route collection and closing reconciliation.",
            new[] { "retail" }, null, PlanTier.Professional),

        // Gastos y utilidad mensual (ventas − gastos). Mismo criterio que contracts: solo retail y Professional.
        new ModuleDefinition("expenses", "Gastos y utilidad", "Expenses & profit",
            "Gastos fijos y sueltos por categoría y utilidad mensual (ventas menos gastos).", "Fixed and one-off expenses by category and monthly profit (sales minus expenses).",
            new[] { "retail", "dealer" }, null, PlanTier.Professional),
        // Calidad del agua: análisis de laboratorio, historial, mantenimiento y página pública de transparencia.
        new ModuleDefinition("quality", "Calidad del agua", "Water quality",
            "Resultados de laboratorio, historial y bitácora de mantenimiento, con una página pública que da confianza a los clientes.", "Lab results, history and maintenance log, with a public page that builds customer trust.",
            new[] { "retail" }, null, PlanTier.Professional),
    };

    private static readonly Dictionary<string, ModuleDefinition> ByToken =
        Modules.ToDictionary(m => m.Token, StringComparer.OrdinalIgnoreCase);

    public static readonly HashSet<string> Whitelist = new(Modules.Select(m => m.Token), StringComparer.OrdinalIgnoreCase);

    public static ModuleDefinition? Get(string token) => ByToken.GetValueOrDefault(token);

    /// <summary>¿El módulo tiene sentido para este tipo de negocio?</summary>
    public static bool IsRelevant(ModuleDefinition m, string? businessType)
    {
        var bt = businessType?.ToLowerInvariant() ?? "";
        if (m.BusinessTypes is { Length: > 0 } && !m.BusinessTypes.Contains(bt, StringComparer.OrdinalIgnoreCase)) return false;
        if (m.ExcludeBusinessTypes is { Length: > 0 } && m.ExcludeBusinessTypes.Contains(bt, StringComparer.OrdinalIgnoreCase)) return false;
        return true;
    }

    /// <summary>
    /// Lo que el negocio TIENE PRENDIDO, sin mirar el plan: null = nunca configurado por un
    /// admin → los módulos que le corresponden por tipo de negocio; "" = explícitamente ninguno;
    /// lista = esa lista (solo tokens válidos). El plan se aplica encima, en ModuleAccess.
    /// </summary>
    public static string[] FilterActive(string? modules, string? businessType = null)
    {
        if (modules is null)
            return Modules.Where(m => IsRelevant(m, businessType)).Select(m => m.Token).ToArray();
        if (modules.Trim().Length == 0) return Array.Empty<string>();

        return modules
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(Whitelist.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
