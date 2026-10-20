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

    // Pedido desde la mesa (QR por mesa, /{slug}?mesa=7). Null = pedido normal (pickup/delivery).
    // Con valor, Channel = "Table". PaymentMethod = "PayAtTable" cuando el cliente eligió pagar
    // al mesero: el pedido queda Pending hasta que el personal lo acepta desde el panel (Pending
    // -> Paid lo manda a cocina, mismo botón que ya existe); si pagó online sigue el flujo Stripe.
    public string? TableNumber { get; set; }

    // Cliente (Customers) al que se enlazó este pedido — por teléfono, o por correo si no hubo
    // teléfono. Null si el cliente no dejó ningún dato de contacto. Sin FK a propósito: es solo
    // para el historial del cliente, y borrar un cliente no debe tocar sus pedidos.
    public Guid? CustomerId { get; set; }

    // Pedido programado: fecha (en la zona horaria del negocio) para la que se pidió, cuando el
    // negocio estaba cerrado al momento de ordenar — apunta a su próxima apertura. Null = pedido
    // para ahora. El menú que se valida es el de ESA fecha, no el de hoy.
    public DateOnly? ScheduledFor { get; set; }

    // Cobro en el local (PaymentMethod = PayAtPickup / PayAtTable): "Paid" significa ACEPTADO, no cobrado.
    // El personal registra el cobro real aquí: CollectedAt (cuándo) + CollectedMethod ("Cash" | "Card" | "Other").
    // Null = por cobrar. Los pedidos que ya pagaron online no lo usan.
    public DateTime? CollectedAt { get; set; }
    public string? CollectedMethod { get; set; }
    // Referencia manual al cobro real cuando el negocio usa su propio POS para cobrar (ej. Toast)
    // en vez de que MaalCa procese el pago — el dinero nunca pasa por aquí, esto es solo una nota
    // para poder conciliar el pedido de MaalCa con el ticket/orden real de ese POS. Opcional, libre.
    public string? CollectedReference { get; set; }

    // Seguimiento público: token aleatorio (128 bits, base64url) para /t/{token} — el cliente ve el estado de su
    // pedido sin login ni correos por cada cambio. EstimatedReadyAt = la hora estimada que el personal fija al aceptar.
    public string? TrackingToken { get; set; }
    public DateTime? EstimatedReadyAt { get; set; }

    // Estación de pedidos (tablet dedicada, alarma hasta que alguien atiende): cuándo el
    // personal reconoció este pedido. Para PayAtPickup/PayAtTable, aceptar (Pending -> Paid)
    // ya lo marca junto con EstimatedReadyAt. Para un pedido que llega directo en Paid (pagado
    // online con Stripe), queda null hasta que alguien lo toca en la estación — así la alarma
    // sabe qué pedidos siguen sin que nadie los vea, sin depender de qué pantalla esté abierta.
    public DateTime? AcknowledgedAt { get; set; }

    // "No vino": el cliente pidió para recoger (pagar al recoger) y nunca vino. El pedido queda
    // Canceled y el cliente suma un NoShow — con eso las reglas de confianza le exigen tarjeta en
    // sus próximos pedidos (ver Affiliate.PickupNoShowRequiresCard). Null = no aplica.
    public DateTime? NoShowAt { get; set; }
}
