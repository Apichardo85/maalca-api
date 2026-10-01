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

    /// <summary>Recibo de pago (backlog documentos/correos, 2026-09-29) — dispara solo cuando
    /// "Marcar pagada" pasa una factura a "Paid" manualmente (cash/transferencia/Zelle) desde
    /// BusinessServices.UpdateInvoiceAsync. A propósito NO se dispara en pagos por Stripe
    /// Checkout (BusinessServices.ConfirmFromWebhookAsync) -- esos ya traen su propio recibo
    /// automático de Stripe si el negocio lo tiene activado; mandar otro acá sería duplicar.
    /// Antes de esto "Marcar pagada" no mandaba nada en absoluto. Solo si el cliente tiene email.</summary>
    Task NotifyInvoicePaidAsync(Invoice invoice, Customer customer, string businessName, string currency);
}
