namespace Maalca.Domain.Enums;

/// <summary>
/// Cómo se comporta un grupo de pantallas sincronizadas (ScreenWall).
/// Mirror = todas las TVs muestran lo mismo al mismo instante (slides y videos alineados).
/// Span = muro: las slides de menú se reparten (una distinta por TV, cambian juntas) y los
/// comerciales con la proporción del muro se estiran sobre todas las TVs como una sola imagen.
/// </summary>
public enum ScreenWallMode
{
    Mirror = 0,
    Span = 1,
}
