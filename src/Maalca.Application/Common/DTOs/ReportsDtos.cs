namespace Maalca.Application.Common.DTOs;

/// <summary>
/// Reportes ampliados de Estadísticas (más allá de visitas/QR/canales de metrics/detailed) —
/// pensado para alimentar gráficos de pastel/barras/línea en el dashboard. Cada lista puede
/// llegar vacía (ej. un negocio sin facturas todavía) — el frontend oculta esa sección en vez de
/// mostrar un gráfico vacío.
/// </summary>
public record BusinessReportsResponse(
    IReadOnlyList<RevenueDayDto> RevenueByDay,
    IReadOnlyList<TopItemDto> TopItems,
    IReadOnlyList<ChannelBreakdownDto> ByChannel,
    IReadOnlyList<PaymentMethodDto> ByPaymentMethod,
    CustomerSegmentDto Customers,
    IReadOnlyList<InvoiceStatusDto> InvoiceStatus,
    IReadOnlyList<StaffActivityDto> StaffActivity,
    string Currency
);

public record RevenueDayDto(string Date, decimal Revenue, int OrdersCount);

/// <summary>Top productos/servicios vendidos (Orders.ItemsJson, pedidos Paid) — restaurante/retail.</summary>
public record TopItemDto(string Name, int Qty, decimal Revenue);

/// <summary>"Online" (storefront público) vs "POS" (mostrador) — Order.Channel.</summary>
public record ChannelBreakdownDto(string Channel, decimal Revenue, int Count);

/// <summary>Cash/Card/Other — solo ventas de POS, que son las únicas con PaymentMethod real.</summary>
public record PaymentMethodDto(string Method, int Count, decimal Revenue);

/// <summary>Clientes con actividad en el período: nuevos (primera visita) vs. recurrentes.</summary>
public record CustomerSegmentDto(int NewCustomers, int ReturningCustomers);

/// <summary>Snapshot de todas las facturas (no filtrado por período — Pendiente/Vencida no "expiran" solas).</summary>
public record InvoiceStatusDto(string Status, int Count, decimal Amount);

/// <summary>Citas/turnos completados por miembro del equipo (Agenda + Fila) — Barbería/Servicios.</summary>
public record StaffActivityDto(string Name, int Count);
