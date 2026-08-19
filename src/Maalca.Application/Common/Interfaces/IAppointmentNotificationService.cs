using Maalca.Domain.Entities;

namespace Maalca.Application.Common.Interfaces;

/// <summary>
/// Tarea #247 — mismo patrón que IOrderNotificationService: maalca-api no manda emails
/// directamente, reusa la infraestructura de Resend que vive en maalca-web llamando a un
/// endpoint interno protegido por secreto compartido. Ver /api/internal/notifications/appointment
/// en maalca-web.
/// </summary>
public interface IAppointmentNotificationService
{
    /// <summary>Cliente reservó por el widget público y dejó su email — dispara desde
    /// PublicBookingService.CreatePublicAppointmentAsync.</summary>
    Task NotifyAppointmentBookedAsync(Appointment appointment, Customer customer, string businessName, string slug, string serviceName, string? staffName);
}
