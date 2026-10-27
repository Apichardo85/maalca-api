namespace Maalca.Application.Common.DTOs;

/// <param name="Plan">"Entrepreneur"/"Growth" (default) o "Enterprise"/"Professional".</param>
/// <param name="Interval">"month" (default) o "year".</param>
/// <param name="ExtraScreens">Pantallas extra a contratar desde el inicio (0–20).</param>
public record CreateCheckoutSessionRequest(string SuccessUrl, string CancelUrl, string? Plan = null,
    string? Interval = null, int ExtraScreens = 0);

/// <summary>Cambia la cantidad de pantallas extra de la suscripción activa (0 = ninguna).</summary>
public record UpdateExtraScreensRequest(int ExtraScreens);

/// <summary>Un precio del catálogo de Stripe, identificado por su lookup_key (maalca_*).</summary>
/// <summary>Precio de Stripe. Amount = precio de la primera unidad. Tiers solo viene en precios
/// escalonados (ej. pantalla extra: $15 la primera, $10 desde la segunda).</summary>
public record BillingPriceDto(string LookupKey, decimal Amount, string? Interval,
    IReadOnlyList<BillingPriceTierDto>? Tiers = null);

/// <summary>Tramo de un precio escalonado: UpTo = hasta qué cantidad aplica (null = sin tope).</summary>
public record BillingPriceTierDto(long? UpTo, decimal Amount);

/// <summary>
/// Todo lo que el dashboard necesita para la sección de plan: nombre público, estado, intervalo,
/// pantallas (incluidas/extra/usadas) y los precios vigentes leídos de Stripe.
/// </summary>
public record BillingSummaryDto(
    string Plan,              // "Free" | "Entrepreneur" | "Enterprise" (interno)
    string PlanName,          // "Starter" | "Growth" | "Professional" (público)
    string PlanStatus,        // "Active" | "PastDue" | "Canceled" | "DowngradedLocked"
    string? Interval,         // "month" | "year" | null
    bool HasSubscription,
    int IncludedScreens,
    int ExtraScreens,
    int ScreenAllowance,
    int ScreensUsed,
    bool SetupFeeDue,
    IReadOnlyList<BillingPriceDto> Prices
);

public record CheckoutSessionResponseDto(string Url);

public record CreatePortalSessionRequest(string ReturnUrl);

public record PortalSessionResponseDto(string Url);

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
    bool DetailsSubmitted,
    string? Country = null
);
