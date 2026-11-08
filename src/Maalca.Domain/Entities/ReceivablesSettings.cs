using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Ajustes de cobros e impuestos de las facturas de reparto/contratos, uno por negocio (sin fila = valores por defecto).
/// Separado del impuesto de venta de la tienda (Affiliate.SalesTaxRate), que es por estado y solo aplica a pedidos en línea.
/// </summary>
public class ReceivablesSettings : BaseEntity
{
    public Guid AffiliateId { get; set; }
    /// <summary>"issue" = días desde la emisión de la factura (por defecto) | "due" = días desde su vencimiento.</summary>
    public string AgingBasis { get; set; } = "issue";
    /// <summary>"none" = sin impuesto | "added" = el precio no lo incluye y se suma | "included" = el precio ya lo incluye.</summary>
    public string TaxMode { get; set; } = "none";
    /// <summary>Tasa en % (ej. 18 para ITBIS). Solo se usa si TaxMode no es "none".</summary>
    public decimal TaxRate { get; set; } = 0m;
    /// <summary>Nombre que se muestra del impuesto (ITBIS, IVA…).</summary>
    public string TaxLabel { get; set; } = "ITBIS";

    public Affiliate? Affiliate { get; set; }
}
