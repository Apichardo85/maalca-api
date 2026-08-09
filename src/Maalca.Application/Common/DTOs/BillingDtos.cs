namespace Maalca.Application.Common.DTOs;

public record CreateCheckoutSessionRequest(string SuccessUrl, string CancelUrl);

public record CheckoutSessionResponseDto(string Url);

// ── Stripe Connect: cuenta de pago del afiliado (destino donde recibe el dinero
// de SUS clientes) — distinto de la suscripción MaalCa→afiliado de arriba. ──

/// <param name="ReturnUrl">A dónde vuelve el afiliado al completar el onboarding en Stripe.</param>
/// <param name="RefreshUrl">A dónde vuelve el afiliado si el link expiró o falló — debe volver a pedir uno nuevo.</param>
public record CreateConnectOnboardingLinkRequest(string ReturnUrl, string RefreshUrl);

public record ConnectOnboardingLinkResponseDto(string Url);

public record ConnectAccountStatusDto(
    bool Connected,
    bool ChargesEnabled,
    bool PayoutsEnabled,
    bool DetailsSubmitted
);
