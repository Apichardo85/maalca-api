using Maalca.Domain.Common;

namespace Maalca.Domain.Entities;

/// <summary>
/// Una obra de un negocio tipo Creator (autor): novela, poemario, cuentos, microrrelatos, ensayo.
/// Tabla propia, mismo patrón que VehicleListing: CRUD en /space/[slug]/works y endpoint público.
///
/// Los valores "token" (Kind, Status, ReadingAccess) son claves en inglés en minúscula; la web las
/// traduce. La lista permitida la valida WorkService, no la base de datos.
///
/// Lectura en el sitio (la "biblioteca"):
///   - ReadingAccess = "free"   → todos los capítulos se leen gratis.
///   - ReadingAccess = "sample" → solo los capítulos marcados IsSample.
///   - ReadingAccess = "members" → las muestras para todos; la obra completa para los miembros del club (ReaderClub.cs).
///   - ReadingAccess = "closed" → ninguno (solo ficha y enlaces de compra).
///   - FreeFrom/FreeUntil abren la obra completa por un tiempo, sin importar ReadingAccess
///     ("dejar leer gratis por momentos"). Cerrada la ventana, vuelve a su ReadingAccess.
/// El SERVIDOR decide qué capítulo se puede leer: un capítulo cerrado nunca viaja en el JSON público.
/// </summary>
public class Work : AuditableEntity
{
    public Guid AffiliateId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? Subtitle { get; set; }
    /// <summary>Segmento de URL de la obra dentro del espacio (/{affiliateSlug}/obras/{Slug}). Único por afiliado.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>"novel" | "poetry" | "stories" | "micro" | "essay" | "chronicle" | "other".</summary>
    public string Kind { get; set; } = "novel";
    /// <summary>"draft" | "published". Borrador nunca sale en la página pública.</summary>
    public string Status { get; set; } = "draft";

    /// <summary>Sinopsis en HTML enriquecido (saneada al guardar con HtmlScrub).</summary>
    public string? Synopsis { get; set; }
    public string? SynopsisEn { get; set; }
    public string? CoverUrl { get; set; }

    public int? Year { get; set; }
    public string? Isbn { get; set; }
    /// <summary>Sello que la publica (ej. "Duende Ediciones"). Texto libre: un autor puede tener obras con otros sellos.</summary>
    public string? Imprint { get; set; }
    /// <summary>Aviso de contenido sensible que la web muestra antes de leer. null = sin aviso.</summary>
    public string? ContentNotice { get; set; }

    /// <summary>JSON array de { label, url } — dónde comprarla (Amazon, librería, tienda propia).</summary>
    public string? BuyLinks { get; set; }

    /// <summary>"free" | "sample" | "members" | "closed".</summary>
    public string ReadingAccess { get; set; } = "sample";
    public DateTime? FreeFrom { get; set; }
    public DateTime? FreeUntil { get; set; }

    public bool Featured { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    public bool IsDemo { get; set; } = false;

    /// <summary>
    /// Versión de la ficha (concurrencia optimista). Sube solo al guardar la ficha (UpdateAsync), no al tocar
    /// capítulos: así cargar poemas y luego guardar la ficha no da un falso conflicto. El dashboard manda la
    /// que leyó (ExpectedRevision) y, si otro guardó en medio, el API responde 409 en vez de pisar sus cambios.
    /// </summary>
    public int Revision { get; set; } = 0;

    public Affiliate? Affiliate { get; set; }
    public List<WorkChapter> Chapters { get; set; } = new();
}

/// <summary>Un capítulo (o poema, o cuento) de una obra. Number es el orden de lectura, único por obra.</summary>
public class WorkChapter : BaseEntity
{
    public Guid WorkId { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>Texto en HTML enriquecido (saneado al guardar con HtmlScrub).</summary>
    public string? ContentHtml { get; set; }
    /// <summary>true = se puede leer gratis cuando la obra está en ReadingAccess "sample".</summary>
    public bool IsSample { get; set; } = false;
    /// <summary>Calculado al guardar; la web lo usa para el tiempo de lectura.</summary>
    public int WordCount { get; set; } = 0;

    public Work? Work { get; set; }
}
