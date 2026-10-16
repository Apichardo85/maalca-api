namespace Maalca.Domain.Enums;

/// <summary>
/// Plantilla visual del Menu Board (cómo se presenta la información en la TV). Classic = 0 es la
/// grilla 3x2 que ya existía, así que todo negocio/pantalla actual se ve igual hasta que elija otra.
/// Los colores NO van aquí: todas las plantillas se tiñen con la paleta del negocio
/// (Primary/Secondary/Accent) y respetan BoardTheme (Dark/Light).
/// </summary>
public enum BoardLayout
{
    Classic = 0,   // grilla 3x2 original
    Hero = 1,      // plato héroe a pantalla dividida + precio grande
    Grid = 2,      // tarjetas con foto grande
    Editorial = 3, // foto grande + lista de especiales
    Carousel = 4,  // 3 tarjetas redondeadas
    Plates = 5,    // platos flotando, precio grande (estilo QSR)
    Bold = 6,      // título gigante + sticker de precio
}
