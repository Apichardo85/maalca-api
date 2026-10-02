using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Inscripción pública en un negocio Comunidad: o bien alguien que quiere ser VOLUNTARIO en una
/// causa de tipo "time" (Kind = "volunteer", CausaId), o bien alguien que se anota a un EVENTO
/// (Kind = "event", ActivityId, con cupo opcional en Activity.Capacity). Un solo registro para
/// ambos porque el panel los gestiona en la misma lista y comparten datos de contacto, estado y
/// avisos.
///
/// Sigue el patrón de TableReservation/Order: datos de contacto inline (flujo público anónimo,
/// sin historial de cliente) y sin FK a la causa/actividad -- guarda un snapshot del título para
/// que la fila siga siendo legible aunque el evento o la causa se borren después.
/// </summary>
public class CommunitySignup : AuditableEntity
{
    public Guid AffiliateId { get; set; }

    /// <summary>"volunteer" | "event".</summary>
    public string Kind { get; set; } = "volunteer";

    public Guid? CausaId { get; set; }
    public Guid? ActivityId { get; set; }

    /// <summary>Título de la causa/evento al momento de inscribirse (snapshot).</summary>
    public string TargetTitle { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>Cuántas personas ocupan lugar (eventos: la persona + acompañantes). Voluntarios: 1.</summary>
    public int PartySize { get; set; } = 1;

    /// <summary>Mensaje o disponibilidad escrita por quien se inscribe.</summary>
    public string? Notes { get; set; }

    /// <summary>"es" | "en" -- idioma de la página al inscribirse, para escribirle en ese idioma.</summary>
    public string Language { get; set; } = "es";

    /// <summary>
    /// "New" (voluntario por contactar) | "Confirmed" (evento: lugar asegurado; voluntario: ya
    /// coordinado) | "Cancelled" (libera el cupo).
    /// </summary>
    public string Status { get; set; } = "New";

    public Affiliate? Affiliate { get; set; }
}
