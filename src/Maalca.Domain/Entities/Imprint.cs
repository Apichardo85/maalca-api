using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Contrato entre un sello (Affiliate de tipo Publisher, ej. Duende Ediciones) y un autor (Affiliate de tipo
/// Creator, ej. CiriWhispers). El sello es su propio espacio: su login, su editor, su Stripe y su catálogo.
/// El autor conserva el suyo. Este registro los enlaza por llave, nunca por nombre.
///
/// Flujo: el sello invita (invited) → el autor acepta (active) o rechaza (declined). El sello puede
/// terminarlo (ended); al terminar, las obras de ese autor dejan de salir con el sello (Work.ImprintId = null).
/// Regla que sostiene todo lo demás: Work.ImprintId apunta a un sello solo si hay contrato activo con su autor
/// (o si la obra es del propio sello). Lo valida WorkService al guardar.
///
/// Regalías y derechos se guardan ya para que el reparto de ventas (Stripe Connect) los lea después.
/// </summary>
public class ImprintAuthor : AuditableEntity
{
    public Guid ImprintId { get; set; }
    public Guid AuthorId { get; set; }

    /// <summary>"invited" | "active" | "declined" | "ended".</summary>
    public string Status { get; set; } = "invited";

    /// <summary>Porcentaje del neto de cada venta que va al autor.</summary>
    public decimal RoyaltyEbookPercent { get; set; }
    public decimal RoyaltyPrintPercent { get; set; }
    /// <summary>Derechos que el autor cede, separados por coma: "ebook,print,audio,translation".</summary>
    public string Rights { get; set; } = "ebook";

    public DateTime? StartsOn { get; set; }
    public DateTime? EndsOn { get; set; }
    public string? Notes { get; set; }

    public DateTime? RespondedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public Affiliate? Imprint { get; set; }
    public Affiliate? Author { get; set; }
}
