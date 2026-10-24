using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Una TV (aparato físico) que tiene abierto el board de un negocio. La identifica un DeviceId
/// que el propio board genera y guarda en la TV (localStorage), así se distingue cuántos aparatos
/// muestran cada pantalla. Se crea/actualiza con la señal de vida que manda el board cada minuto
/// (POST /api/public/affiliates/{slug}/screen-heartbeat) y la vigila ScreenMonitorWorker, que avisa
/// a MaalCa cuando una TV deja de reportar en horario de atención.
/// </summary>
public class ScreenDevice : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public Affiliate? Affiliate { get; set; }

    /// Pantalla extra que muestra (null = pantalla principal /{slug}/board).
    public Guid? ScreenId { get; set; }

    public string DeviceId { get; set; } = string.Empty;
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    /// document.visibilityState == "visible" en la última señal (false = la página quedó detrás de otra app).
    public bool Visible { get; set; } = true;
    /// Qué estaba en pantalla: "menu" | "combo" | "ad" (+ etiqueta legible).
    public string? ShowingKind { get; set; }
    public string? ShowingLabel { get; set; }
    public string? Viewport { get; set; }
    public string? UserAgent { get; set; }
    public string? AppVersion { get; set; }

    /// Comando pendiente para la TV ("reload" | "identify"); la TV lo recoge en su siguiente señal.
    public string? PendingCommand { get; set; }
    public DateTime? CommandAt { get; set; }

    /// Fecha del aviso de caída abierto (null = sin incidente abierto).
    public DateTime? AlertSentAt { get; set; }

    /// Aparato retirado a propósito: no se vigila ni se alerta (reversible desde el panel).
    public bool Archived { get; set; }
}
