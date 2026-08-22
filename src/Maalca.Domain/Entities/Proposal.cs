using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Propuesta de servicio (Profesional/Servicios) — un documento simple con alcance + precio que
/// el negocio envía a un cliente potencial y este puede aceptar en línea. Deliberadamente NO usa
/// Invoice: una propuesta pasa por revisión/aceptación ANTES de que haya trabajo que facturar —
/// mezclarla con Invoice forzaría estados como "Pending" a significar cosas distintas según de
/// dónde viene. Firma = "acepto escribiendo mi nombre" (no un dibujo/certificado legal real),
/// mismo nivel de simplicidad que el resto del flujo de reserva pública del proyecto.
/// </summary>
public class Proposal : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    // Draft (aún no enviada) / Sent (esperando al cliente) / Accepted / Declined / Expired
    public string Status { get; set; } = "Draft";
    // Token público — la URL /propuesta/{token} no requiere login, así que no puede ser el Id
    // secuencial-friendly de siempre expuesto sin más pensarlo dos veces; se genera aparte para
    // poder rotarlo si hiciera falta sin tocar el Id real de la fila.
    public Guid Token { get; set; } = Guid.NewGuid();
    public DateTime? SentAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public string? AcceptedByName { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Notes { get; set; }
    // CRM (tarea #244) — resuelto/creado por teléfono cuando el negocio la crea, si trae
    // CustomerPhone. Nullable: una propuesta a un prospecto sin teléfono aún no puede vincularse.
    public Guid? CustomerId { get; set; }

    // Documento adjunto (tarea #336) — PDF/imagen que el negocio sube al crear la propuesta
    // (contrato, cotización detallada, referencia visual). Sube a Supabase Storage igual que
    // las imágenes de catálogo; acá solo se guarda la URL pública + nombre original para mostrar
    // el link "Ver documento adjunto" tanto en el dashboard como en la página pública.
    public string? AttachmentUrl { get; set; }
    public string? AttachmentName { get; set; }

    // Auditoría de firma (tarea #334) — capturados server-side desde HttpContext en el momento
    // de aceptar (nunca confiados del body del cliente), para dejar rastro de quién/desde dónde
    // se aceptó. No es una firma certificada legalmente, pero refuerza la trazabilidad de
    // "acepto escribiendo mi nombre".
    public string? AcceptedIp { get; set; }
    public string? AcceptedUserAgent { get; set; }

    public Affiliate? Affiliate { get; set; }
    public Customer? Customer { get; set; }
}
