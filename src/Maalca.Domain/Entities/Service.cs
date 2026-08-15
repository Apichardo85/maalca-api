using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

public class Service : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    public decimal Price { get; set; }
    // Nullable a propósito — null = el dueño no fijó duración, se oculta en catálogo/canales
    // públicos. Ver CatalogCrudService.UpdateServiceAsync para el sentinel de "vaciar" (0)
    // y PublicBookingService para el fallback usado solo al calcular horarios de Agenda.
    public int? DurationMinutes { get; set; }
    public string? Category { get; set; }
    public string? ImageUrl { get; set; }
    // Galería — JSON array de URLs, orden = orden de visualización. ImageUrl se mantiene
    // sincronizado con Images[0] (o null si Images queda vacío) para que nada que ya lea
    // ImageUrl directamente (templates públicos, MenuBoard, etc.) se entere de este cambio.
    public string? Images { get; set; }
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = "Active";
    public bool IsPubliclyVisible { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    public bool IsDemo { get; set; } = false;

    public Affiliate? Affiliate { get; set; }
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
