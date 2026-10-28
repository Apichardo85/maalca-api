using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Override de una cadena del sitio de MaalCa (maalca.com), editable desde /ops/sitio sin
/// desplegar. Key = la misma clave de traducción de la web (ej. "home.hero.title1"); si no hay
/// fila para una clave, la web usa su texto por defecto (hardcodeado en useSimpleLanguage.tsx).
///
/// Es contenido de la PLATAFORMA, no de un afiliado (sin AffiliateId). Mismo patrón que
/// PlatformAddOn: lo que servimos a los negocios (editar su contenido sin tocar código), ahora
/// para nosotros — el antídoto al "cuchillo de palo".
/// </summary>
public class PlatformContent : BaseEntity
{
    /// <summary>Clave de traducción (ej. "home.hero.title1"). Única.</summary>
    public string Key { get; set; } = string.Empty;

    public string? Es { get; set; }
    public string? En { get; set; }

    /// <summary>Email del admin de plataforma que lo editó por última vez.</summary>
    public string? UpdatedBy { get; set; }
}
