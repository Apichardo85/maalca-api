using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Aviso para el DUEÑO/EQUIPO del negocio (no para el cliente final): pedido nuevo, reserva por
/// aceptar, cita por confirmar, factura pagada, propuesta aceptada. Es del negocio, no de cada
/// usuario: si alguien del equipo lo marca como leído, queda leído para todos.
/// Alimenta los badges y la campana de /space, y el Web Push. Se guarda en español e inglés porque
/// el panel y el push se muestran en el idioma de quien lo lee.
/// </summary>
public class OwnerNotification : AuditableEntity
{
    public Guid AffiliateId { get; set; }

    /// <summary>"order" | "reservation" | "appointment" | "invoice_paid" | "proposal_accepted".</summary>
    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string TitleEn { get; set; } = string.Empty;
    public string? BodyEn { get; set; }

    /// <summary>Ruta relativa dentro de /space/{slug}/ a la que lleva el aviso (ej. "orders", "reservations").</summary>
    public string? Url { get; set; }

    /// <summary>Id del pedido/reserva/cita/factura/propuesta que originó el aviso.</summary>
    public Guid? EntityId { get; set; }

    public DateTime? ReadAt { get; set; }

    public Affiliate? Affiliate { get; set; }
}

/// <summary>
/// Dispositivo/navegador que aceptó Web Push para un negocio. Una fila por endpoint (único): el mismo
/// navegador que se re-suscribe actualiza su fila en vez de duplicarla. Los endpoints que el servicio
/// de push reporta como expirados (404/410) se borran al enviar.
/// </summary>
public class PushSubscription : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;

    /// <summary>"es" | "en" — idioma del panel de quien activó el aviso.</summary>
    public string Lang { get; set; } = "es";

    public Affiliate? Affiliate { get; set; }
}
