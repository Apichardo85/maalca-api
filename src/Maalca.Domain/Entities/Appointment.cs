using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

public class Appointment : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ServiceId { get; set; }
    public DateTime Date { get; set; }
    public string Time { get; set; } = string.Empty; // HH:mm format
    public string Status { get; set; } = "Scheduled"; // Scheduled, Confirmed, InProgress, Completed, Cancelled, NoShow
    public string? Notes { get; set; }
    public Guid? AssignedToId { get; set; } // Team member
    // Task #193 — null = todavía no se envió recordatorio. Se marca con UtcNow cuando el cron
    // de maalca-web confirma que envió el correo, así el próximo barrido no lo vuelve a mandar.
    public DateTime? ReminderSentAt { get; set; }
    // Tarea #246 — token público para "gestiona tu cita" (confirmar/reagendar/cancelar sin
    // login). Mismo patrón que Proposal.Token: no es el Id secuencial-friendly de siempre
    // expuesto sin pensarlo, se genera aparte para poder rotarlo sin tocar el Id real de la fila.
    public Guid Token { get; set; } = Guid.NewGuid();

    public Affiliate? Affiliate { get; set; }
    public Customer? Customer { get; set; }
    public Service? Service { get; set; }
    public TeamMember? AssignedTo { get; set; }
}
