using Maalca.Domain.Entities;

namespace Maalca.Application.Common.Interfaces;

/// <summary>
/// Mismo patrón que IReservationNotificationService: maalca-api no manda correos, llama a un
/// endpoint interno de maalca-web protegido por secreto compartido (Resend vive allá). Ver
/// /api/internal/notifications/store-account-approved en maalca-web.
/// </summary>
public interface IStoreAccountNotificationService
{
    /// <summary>
    /// El negocio aprobó una cuenta de cliente/mayorista. La tienda le promete al cliente
    /// "te avisamos cuando te aprueben", así que hay que cumplirlo: sin esto se queda esperando
    /// un correo que nunca llega. Nunca lanza: un correo que no salió no debe deshacer la
    /// aprobación, que ya está guardada.
    /// </summary>
    Task NotifyApprovedAsync(StoreCustomer account, Affiliate affiliate, string? priceListName);
}
