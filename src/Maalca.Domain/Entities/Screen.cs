using Maalca.Domain.Common;
using Maalca.Domain.Enums;

namespace Maalca.Domain.Entities;

/// <summary>
/// Fase 9 Etapa B — una pantalla adicional del negocio, con su propia URL pública
/// (/{slug}/board/{screenId}). La pantalla "base" (/{slug}/board sin id) NO es una fila
/// de esta tabla — sigue siendo, como antes de Etapa B, los campos del propio Affiliate
/// (Language/BoardTheme/AdFrequency). Screen solo existe para pantallas EXTRA que un
/// negocio agrega (ej. una segunda TV mostrando solo comerciales, o una en inglés).
///
/// Cada override es nullable: null = hereda el default del Affiliate para ese campo.
/// Comerciales (ScreenAd) siguen siendo un pool compartido por afiliado, no por pantalla —
/// todas las pantallas del mismo negocio muestran los mismos comerciales, solo cambia cada
/// cuánto aparecen (AdFrequency) y qué categorías de menú se ven (CategoryFilter).
/// </summary>
public class Screen : BaseEntity
{
    public Guid AffiliateId { get; set; }
    public Affiliate? Affiliate { get; set; }

    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;

    public string? Language { get; set; }
    public BoardTheme? BoardTheme { get; set; }
    public int? AdFrequency { get; set; }

    /// Nombres de categoría separados por coma — null/vacío = todas las categorías (igual que
    /// hoy). Mismo formato simple que Affiliate.Modules, sin tabla aparte todavía.
    public string? CategoryFilter { get; set; }
}
