using Maalca.Domain.Common;
using Maalca.Domain.Enums;

namespace Maalca.Domain.Entities;

/// <summary>
/// Grupo de pantallas (Screen) sincronizadas — "muro de TVs". Se configura entero desde el
/// dashboard (Pantallas): cada TV usa la URL fija de su Screen (/{slug}/board/{screenId}) y el
/// board recibe en el catálogo público su modo y su posición en el muro (WallCol/WallRow).
///
/// El CONTENIDO del muro (categorías, comerciales, tema, plantilla, idioma) sale de la pantalla
/// líder — la de la posición (0,0) —: todas las TVs deben tener exactamente la misma lista de
/// slides para que el timeline determinista (maalca-web src/lib/wall-sync.ts) coincida.
/// </summary>
public class ScreenWall : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public Affiliate? Affiliate { get; set; }

    public string Name { get; set; } = string.Empty;
    public ScreenWallMode Mode { get; set; } = ScreenWallMode.Span;

    /// En Mirror solo importa Cols*Rows (cuántas TVs); en Span es el acomodo físico.
    public int Cols { get; set; } = 2;
    public int Rows { get; set; } = 1;

    /// Ancho del marco físico entre TVs, en px de pantalla: la imagen "salta" el marco.
    public int BezelPx { get; set; } = 0;

    /// Mientras esté activo, todas las TVs del muro muestran la pantalla de calibración
    /// (color por segundo, destello, barra que cruza el muro) en vez del menú.
    public bool Calibrate { get; set; } = false;
}
