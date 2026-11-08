using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Última ubicación conocida de una ruta en curso, enviada desde el teléfono del conductor (modo camión).
/// Una sola fila por ruta (se sobrescribe); no es un historial de recorrido.
/// </summary>
public class VehiclePosition : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public Guid RouteId { get; set; }
    public decimal Lat { get; set; }
    public decimal Lng { get; set; }
    public double? AccuracyMeters { get; set; }
    public double? SpeedKmh { get; set; }
    /// <summary>Momento en que el teléfono tomó la posición (UTC).</summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
