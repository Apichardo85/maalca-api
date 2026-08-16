using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Reserva de mesa — deliberadamente separada de Appointment. Appointment modela una cita 1:1
/// con un miembro del equipo (barbería, consulta de servicio profesional); una reserva de
/// restaurante no tiene "servicio" ni "quién atiende", tiene cuántas personas y a qué hora.
/// Antes de esto, Restaurant.tsx reutilizaba el mismo flujo de Appointment (PublicBookingSection),
/// forzando a un comensal a "elegir un servicio" y opcionalmente un miembro del equipo para poder
/// reservar mesa. Ver docs/audits/business-type-flows-audit.md — este era el ejemplo canónico de
/// un objeto sirviendo significados que no encajan entre sí.
///
/// Sigue el patrón de Order (datos de cliente inline, sin FK a Customer) en vez del de Appointment
/// (Customer reutilizado por teléfono) porque, igual que un pedido, es un flujo público anónimo
/// donde no hace falta historial de cliente para operar — solo saber a quién sentar y cuándo.
/// </summary>
public class TableReservation : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public DateTime Date { get; set; }
    public string Time { get; set; } = string.Empty; // HH:mm
    public int PartySize { get; set; } = 2;
    // Requested (pública, sin confirmar aún) / Confirmed / Seated / Completed / Cancelled / NoShow
    public string Status { get; set; } = "Requested";
    public string? Notes { get; set; }

    public Affiliate? Affiliate { get; set; }
}
