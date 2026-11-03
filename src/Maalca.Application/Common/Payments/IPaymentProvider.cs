using Maalca.Application.Common.DTOs;
using Maalca.Domain.Entities;

namespace Maalca.Application.Common.Payments;

/// <summary>Una línea del cobro (producto, impuesto, propina, envío). UnitAmount en la moneda del pedido (no en centavos).</summary>
public record PaymentLine(string Name, decimal UnitAmount, int Quantity);

public record PaymentCheckoutRequest(
    Guid OrderId,
    string Currency,
    IReadOnlyList<PaymentLine> Lines,
    string SuccessUrl,
    string CancelUrl,
    string? CustomerEmail = null,
    DateTime? ExpiresAt = null,   // caducidad de la sesión de pago (null = la del proveedor)
    bool CollectPhone = false);   // pedir el teléfono en la página de pago

/// <param name="CheckoutId">Id de la sesión en el proveedor — se guarda en el pedido para confirmar el pago.</param>
/// <param name="Url">A dónde redirigir al cliente para pagar.</param>
public record PaymentCheckoutResult(string CheckoutId, string? Url);

public record PaymentCheckoutStatus(bool Paid, string? PaymentReference, string? CustomerName, string? CustomerEmail, string? CustomerPhone);

/// <summary>
/// Pasarela de cobro de un negocio. Hoy solo existe Stripe Connect (StripePaymentProvider); Clover o Square
/// entran como otra implementación sin tocar OrderService. El dinero siempre va directo a la cuenta del negocio.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>"stripe" | (futuro) "clover" | "square".</summary>
    string Key { get; }

    /// <summary>¿El negocio terminó de conectar esta pasarela y puede cobrar?</summary>
    bool IsReady(Affiliate affiliate);

    Task<PaymentCheckoutResult> CreateCheckoutAsync(Affiliate affiliate, PaymentCheckoutRequest request);

    Task<PaymentCheckoutStatus> GetCheckoutStatusAsync(Affiliate affiliate, string checkoutId);
}

/// <summary>Elige la pasarela de un negocio. Hoy siempre Stripe; cuando exista una segunda, decide por la configuración del negocio.</summary>
public interface IPaymentProviderResolver
{
    IPaymentProvider For(Affiliate affiliate);
}

public static class PaymentLines
{
    /// <summary>
    /// Líneas del cobro de un pedido: cada producto (con sus notas en el nombre) y luego Impuesto, Propina y Envío
    /// como líneas aparte solo si son > 0 — así salen claras en el recibo y en el reporte de la cuenta del negocio.
    /// </summary>
    public static List<PaymentLine> For(
        IEnumerable<OrderItemDto> items, decimal tax, decimal tip, decimal shipping = 0, string shippingLabel = "Shipping")
    {
        var lines = items
            .Select(i => new PaymentLine(string.IsNullOrWhiteSpace(i.Notes) ? i.Name : $"{i.Name} ({i.Notes})", i.Price, i.Qty))
            .ToList();
        if (tax > 0) lines.Add(new PaymentLine("Tax", tax, 1));
        if (tip > 0) lines.Add(new PaymentLine("Tip", tip, 1));
        if (shipping > 0) lines.Add(new PaymentLine(shippingLabel, shipping, 1));
        return lines;
    }
}
