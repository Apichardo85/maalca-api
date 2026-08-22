using Maalca.Domain.Entities;

namespace Maalca.Application.Common.Interfaces;

/// <summary>
/// Mismo patrón que IInvoiceNotificationService/IAppointmentNotificationService: maalca-api no
/// manda emails directamente, reusa la infraestructura de Resend en maalca-web llamando a un
/// endpoint interno protegido por secreto compartido. Ver /api/internal/notifications/proposal
/// en maalca-web.
/// </summary>
public interface IProposalNotificationService
{
    /// <summary>Se marcó la propuesta como "Sent" — dispara desde ProposalService.SendProposalAsync,
    /// solo si el cliente tiene email guardado (si no, el dueño sigue copiando el link a mano).</summary>
    Task NotifyProposalSentAsync(Proposal proposal, string businessName, string proposalLink);

    /// <summary>El cliente aceptó/firmó la propuesta (tarea #338) — avisa al NEGOCIO, no al
    /// cliente. Dispara desde ProposalService.AcceptPublicProposalAsync, solo si el afiliado
    /// tiene ContactEmail configurado (si no, el dueño se entera al entrar al dashboard igual).</summary>
    Task NotifyProposalAcceptedAsync(Proposal proposal, Affiliate affiliate);
}
