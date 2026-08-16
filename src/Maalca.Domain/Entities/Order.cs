using Maalca.Domain.Common;
using Maalca.Domain.Enums;

namespace Maalca.Domain.Entities;

/// <summary>
/// Pedido real del storefront público de un afiliado — reemplaza/complementa el flujo de
/// WhatsApp-only de CartDrawer.tsx. ItemsJson guarda una copia congelada del carrito al
/// momento del pedido (nombre/precio/qty) — nunca se re-resuelve contra el catálogo en vivo,
/// así un cambio de precio posterior no altera pedidos ya hechos.
/// </summary>
public class Order : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public Affiliate? Affiliate { get; set; }

    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public string? Notes { get; set; }

    /// JSON array: [{ "itemId": "...", "name": "...", "price": 0.0, "qty": 1 }]
    public string ItemsJson { get; set; } = "[]";

    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    // Propina — Restaurante. Se suma al Total (Total = Subtotal + Tax + Tip) y viaja como línea
    // aparte en el Checkout de Stripe (mismo patrón que Tax) para que aparezca clara en el recibo
    // y en el reporte de la cuenta Connect del negocio, en vez de mezclarse en el precio de los
    // items. 0 por default — nunca obligatoria.
    public decimal Tip { get; set; } = 0;
    public decimal Total { get; set; }
    public string Currency { get; set; } = "USD";

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    // Cobro con Stripe (direct charge contra la cuenta Connect del afiliado) — null si el
    // pedido se hizo por el flujo de WhatsApp sin cobro online.
    public string? StripeCheckoutSessionId { get; set; }
    public string? StripePaymentIntentId { get; set; }

    // POS (Etapa D) — de dónde vino el pedido: "Online" (storefront público, default) o "POS"
    // (registrado a mano desde el dashboard, venta presencial). PaymentMethod solo aplica a
    // POS por ahora: "Cash" | "Card" | "Other" — el POS registra el cobro, no lo procesa (fase
    // 1, sin hardware lector todavía).
    public string Channel { get; set; } = "Online";
    public string? PaymentMethod { get; set; }
}
