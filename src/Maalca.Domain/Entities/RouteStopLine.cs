using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Una línea planificada de una parada: lo que el cliente dijo que iba a entregar o recibir
/// ("3 cajas", "1 estufa"). Es el estimado; lo real queda en Delivery/DeliveryItem al registrar la
/// parada. Comparar ambos es lo que evita que una ruta "se descuadre".
/// </summary>
public class RouteStopLine : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public Guid RouteStopId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public string? Notes { get; set; }
    public int SortOrder { get; set; }

    public RouteStop? RouteStop { get; set; }
}
