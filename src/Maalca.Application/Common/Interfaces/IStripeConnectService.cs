using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>
/// Cuenta conectada de Stripe (Standard, direct charges) que el afiliado usa para recibir
/// pagos de SUS PROPIOS clientes. No confundir con IStripeBillingService, que maneja la
/// suscripción MaalCa→afiliado. Ver plans/spec-maalca-api-espacio-v2.md, Fase de Pagos.
/// </summary>
public interface IStripeConnectService
{
    /// <summary>
    /// Crea (si no existe) la cuenta conectada del afiliado en Stripe y devuelve un link de
    /// onboarding de un solo uso. Si la cuenta ya existe, solo genera el link (reutiliza la cuenta).
    /// </summary>
    Task<ConnectOnboardingLinkResponseDto> CreateOnboardingLinkAsync(Guid affiliateId, CreateConnectOnboardingLinkRequest request);

    /// <summary>
    /// Estado actual de la cuenta conectada, consultado en vivo contra Stripe (no solo el cache
    /// local) — se llama al abrir la pantalla de "Recibir pagos" en el dashboard.
    /// </summary>
    Task<ConnectAccountStatusDto> GetStatusAsync(Guid affiliateId);

    Task HandleWebhookEventAsync(string json, string signatureHeader);
}
