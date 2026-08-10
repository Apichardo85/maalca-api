namespace Maalca.Domain.Enums;

/// <summary>
/// Cómo se ajusta la imagen/video del comercial dentro del recuadro del slide. Contain = 0 es
/// el default (nunca recorta, deja franjas si la proporción no coincide) — se eligió como
/// default porque el problema reportado fue justo lo contrario: Cover recortaba fotos y hacía
/// ver videos "gigantes" sin control. Cover sigue disponible para quien prefiera pantalla
/// completa a costa de recortar.
/// </summary>
public enum ScreenAdFit
{
    Contain = 0,
    Cover = 1,
}
