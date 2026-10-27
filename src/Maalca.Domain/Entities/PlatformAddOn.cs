using Maalca.Domain.Common;
using Maalca.Domain.Enums;

namespace Maalca.Domain.Entities;

/// <summary>
/// Extra que MaalCa vende sobre sus planes (Kit de pantalla, Servicio completo, pantalla extra,
/// redes, soporte, consultoría). Antes vivía escrito a mano en maalca-web (/servicios); ahora se
/// edita desde /ops/precios sin desplegar. Es catálogo de la plataforma, no de un afiliado: no
/// tiene AffiliateId.
///
/// El precio que se COBRA de verdad sigue saliendo de Stripe para lo que tenga Price ID (hoy solo
/// la pantalla extra); esta tabla es lo que se MUESTRA y lo que /ops edita.
/// </summary>
public class PlatformAddOn : BaseEntity
{
    /// <summary>Identificador estable (ej. "screen-kit"). Único.</summary>
    public string Key { get; set; } = string.Empty;

    public string NameEs { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string DescEs { get; set; } = string.Empty;
    public string DescEn { get; set; } = string.Empty;

    public decimal Price { get; set; }

    /// <summary>Precio por volumen: desde la unidad VolumeFromQty, cada una cuesta VolumePrice.
    /// Ej. pantalla extra: Price 15, VolumePrice 10, VolumeFromQty 2. Null = sin volumen.</summary>
    public decimal? VolumePrice { get; set; }
    public int? VolumeFromQty { get; set; }

    public AddOnUnit Unit { get; set; } = AddOnUnit.Monthly;
    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;

    /// <summary>Email del admin de plataforma que lo editó por última vez.</summary>
    public string? UpdatedBy { get; set; }
}
