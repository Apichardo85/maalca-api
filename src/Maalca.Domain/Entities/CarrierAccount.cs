using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Cuenta del negocio con su agregador de etiquetas (hoy: United Bridge). Guarda la API key CIFRADA (nunca se devuelve
/// al dashboard, solo los últimos 4), el servicio y la caja por defecto, y la dirección de origen de los envíos.
/// Una fila por negocio y proveedor.
/// </summary>
public class CarrierAccount : AuditableEntity
{
    public Guid AffiliateId { get; set; }
    public string Provider { get; set; } = "unitedbridge";
    public string? ApiKeyEnc { get; set; }
    public string? ApiKeyLast4 { get; set; }
    // Código de servicio del proveedor: UPS_GROUND, FEDEX_HOME_DELIVERY, USPS_UB_GA, BEST_UB_SERVICE...
    public string Service { get; set; } = "UPS_GROUND";
    public string Package { get; set; } = "Parcel";
    public decimal LengthIn { get; set; } = 12;
    public decimal WidthIn { get; set; } = 9;
    public decimal HeightIn { get; set; } = 6;
    // Peso de la caja vacía + relleno, se suma al de los productos.
    public decimal BoxWeightOz { get; set; } = 0;
    public string? FromName { get; set; }
    public string? FromPhone { get; set; }
    public string? FromAddress1 { get; set; }
    public string? FromAddress2 { get; set; }
    public string? FromCity { get; set; }
    public string? FromState { get; set; }
    public string? FromZip { get; set; }
    public bool IsActive { get; set; } = true;
}
