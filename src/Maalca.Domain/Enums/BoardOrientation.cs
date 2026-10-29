namespace Maalca.Domain.Enums;

/// <summary>
/// Cómo está montada la TV. Landscape = la de siempre (1920x1080). Portrait = TV girada (1080x1920):
/// el board dibuja un flyer vertical a pantalla completa. Hoy solo cambia algo con BoardLayout.Panels.
/// </summary>
public enum BoardOrientation
{
    Landscape = 0,
    Portrait = 1,
}
