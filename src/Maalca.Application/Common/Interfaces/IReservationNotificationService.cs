using Maalca.Domain.Entities;

namespace Maalca.Application.Common.Interfaces;

/// <summary>
/// Mismo patrón que IInvoiceNotificationService: maalca-api no manda emails directamente, llama
/// a un endpoint interno de maalca-web protegido por secreto compartido (Resend vive allá). Ver
/// /api/internal/notifications/reservation-requested en maalca-web.
/// </summary>
public interface IReservationNotificationService
{
    /// <summary>
    /// Entró una reserva pública de mesa (status "Requested"). Avisa al restaurante
    /// (Affiliate.ContactEmail) y, si el comensal dejó email, le manda acuse de recibo. Nunca
    /// lanza: un correo que no salió no debe tumbar la reserva.
    /// </summary>
    Task NotifyReservationRequestedAsync(TableReservation reservation, Affiliate affiliate);
}
