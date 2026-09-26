namespace Maalca.Application.Common.DTOs;

/// <param name="SuccessUrl">A dónde vuelve el donante si el pago se completó (Checkout mode=payment).</param>
/// <param name="CancelUrl">A dónde vuelve el donante si canceló el pago.</param>
public record CreateDonationRequest(
    decimal Amount,
    string? Currency,
    string? DonorName,
    string? DonorEmail,
    string? Message,
    string SuccessUrl,
    string CancelUrl
);

/// <param name="CheckoutUrl">
/// Null si el afiliado todavía no tiene Stripe Connect activo (ChargesEnabled=false) — el
/// storefront debe caer al link de WhatsApp existente en vez de intentar cobrar.
/// </param>
public record CreateDonationResponseDto(Guid DonationId, string? CheckoutUrl);

/// <param name="RaisedThisMonth">
/// Suma real de donaciones Status=Paid del mes calendario en curso (zona horaria del
/// afiliado) — reemplaza el número que antes se reportaba a mano en
/// Affiliate.CommunityImpact.FundraisingCurrentAmount, solo cuando el afiliado tiene Stripe
/// Connect con cargos habilitados. Null si no lo tiene: el frontend debe caer al valor
/// reportado a mano en ese caso, no mostrar $0 falso.
/// </param>
public record PublicDonationSummaryDto(decimal? RaisedThisMonth);
