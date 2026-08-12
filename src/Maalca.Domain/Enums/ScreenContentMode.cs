namespace Maalca.Domain.Enums;

/// <summary>
/// Fase 9 Etapa C — qué tipo de contenido rota en una pantalla adicional. Menu = 0 preserva el
/// comportamiento que ya existía (menú del catálogo con comerciales intercalados). AdsOnly y
/// FeaturedOnly permiten armar TVs de un solo propósito (ej. una pantalla solo de promos, o
/// una que solo muestra los items marcados como destacados) sin tocar la pantalla base.
/// </summary>
public enum ScreenContentMode
{
    Menu = 0,
    AdsOnly = 1,
    FeaturedOnly = 2,
}
