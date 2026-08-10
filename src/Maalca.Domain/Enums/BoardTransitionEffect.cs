namespace Maalca.Domain.Enums;

/// <summary>
/// Efecto de transición entre slides del Menu Board. Fade = 0 preserva el comportamiento que
/// ya existía (era el único efecto, ahora es uno de varios).
/// </summary>
public enum BoardTransitionEffect
{
    Fade = 0,
    Slide = 1,
    Zoom = 2,
    None = 3,
}
