using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Bloqueo manual de horario — task #192. Antes, el único origen de "horas ocupadas" en la
/// agenda pública/dashboard eran citas ya agendadas (Appointment); no había forma de que el
/// negocio o un profesional marcara indisponibilidad (almuerzo, vacaciones, cierre temprano)
/// sin tener que crear una cita falsa como truco. StaffId null = aplica a TODO el personal
/// (ej. el negocio cierra por una hora); StaffId con valor = solo ese profesional.
/// </summary>
public class TimeBlock : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public Guid? StaffId { get; set; } // null = aplica a todo el personal
    public DateTime Date { get; set; }
    public string StartTime { get; set; } = string.Empty; // HH:mm
    public string EndTime { get; set; } = string.Empty; // HH:mm
    public string? Reason { get; set; }

    public Affiliate? Affiliate { get; set; }
    public TeamMember? Staff { get; set; }
}
