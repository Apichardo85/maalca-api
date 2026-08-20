using Maalca.Domain.Entities;

namespace Maalca.Application.Common.Interfaces;

/// <summary>
/// Mismo patrón que IAppointmentNotificationService/IOrderNotificationService: maalca-api no
/// manda emails directamente, reusa la infraestructura de Resend en maalca-web llamando a un
/// endpoint interno protegido por secreto compartido. Ver /api/internal/notifications/invoice
/// en maalca-web.
/// </summary>
public interface IInvoiceNotificationService
{
    /// <summary>Se generó un link de cobro real (Stripe Checkout) para la factura — dispara desde
    /// InvoiceService.CreateInvoiceCheckoutAsync, solo si el cliente tiene email.</summary>
    Task NotifyInvoicePaymentLinkAsync(Invoice invoice, Customer customer, string businessName, string currency, string paymentLink);
}
