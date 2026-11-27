namespace Maalca.Application.Common.DTOs;

/// <summary>Dónde comprar una obra (Amazon, librería, tienda propia).</summary>
public record WorkBuyLinkDto(string Label, string Url);

// ── Dashboard del dueño ─────────────────────────────────────────────────────────

/// <summary>Una obra en el dashboard (sin el texto de los capítulos).</summary>
public record WorkDto(
    Guid Id,
    string Title,
    string? TitleEn,
    string? Subtitle,
    string Slug,
    string Kind,
    string Status,
    string? Synopsis,
    string? SynopsisEn,
    string? CoverUrl,
    int? Year,
    string? Isbn,
    string? Imprint,
    string? ContentNotice,
    IReadOnlyList<WorkBuyLinkDto> BuyLinks,
    string ReadingAccess,
    DateTime? FreeFrom,
    DateTime? FreeUntil,
    bool Featured,
    int SortOrder,
    bool IsDemo,
    int ChapterCount,
    int WordCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // Versión de la ficha. El dashboard la devuelve como ExpectedRevision al guardar.
    int Revision,
    // Portada en inglés (opcional; sin ella, en inglés se usa CoverUrl).
    string? CoverUrlEn = null
);

/// <summary>Obra + índice de capítulos (sin texto). El texto se pide capítulo por capítulo.</summary>
public record WorkDetailDto(WorkDto Work, IReadOnlyList<WorkChapterRefDto> Chapters);

public record WorkChapterRefDto(Guid Id, int Number, string Title, bool IsSample, int WordCount, DateTime? UpdatedAt);

public record WorkChapterDto(Guid Id, Guid WorkId, int Number, string Title, string? ContentHtml, bool IsSample, int WordCount);

/// <summary>Alta y edición: PUT reemplaza todos los campos editables. Slug vacío = se genera del título.</summary>
public record UpsertWorkRequest(
    string Title,
    string? TitleEn,
    string? Subtitle,
    string? Slug,
    string? Kind,
    string? Status,
    string? Synopsis,
    string? SynopsisEn,
    string? CoverUrl,
    int? Year,
    string? Isbn,
    string? Imprint,
    string? ContentNotice,
    IReadOnlyList<WorkBuyLinkDto>? BuyLinks,
    string? ReadingAccess,
    DateTime? FreeFrom,
    DateTime? FreeUntil,
    bool Featured,
    int SortOrder,
    // Revisión que tenía la ficha cuando se abrió el formulario. Si ya no coincide, 409 (no se pisa).
    // null = sin comprobación (clientes viejos durante el despliegue).
    int? ExpectedRevision = null,
    string? CoverUrlEn = null
);

/// <summary>Number null en el alta = va al final.</summary>
public record UpsertWorkChapterRequest(int? Number, string Title, string? ContentHtml, bool IsSample);

/// <summary>Importar un manuscrito ya partido en capítulos. Mode "append" (al final) o "replace" (borra los actuales).</summary>
public record ImportWorkChaptersRequest(string? Mode, IReadOnlyList<UpsertWorkChapterRequest> Chapters);

/// <summary>Nuevo orden de lectura: todos los ids de capítulos de la obra, en orden.</summary>
public record ReorderWorkChaptersRequest(IReadOnlyList<Guid> ChapterIds);

// ── Página pública ──────────────────────────────────────────────────────────────

/// <summary>Una obra en la lista pública del autor.</summary>
public record PublicWorkSummaryDto(
    Guid Id,
    string Slug,
    string Title,
    string? TitleEn,
    string? Subtitle,
    string Kind,
    string? Synopsis,
    string? SynopsisEn,
    string? CoverUrl,
    int? Year,
    string? Imprint,
    bool Featured,
    int ChapterCount,
    // "full" (se lee entera), "sample" (algunos capítulos) o "none".
    string ReadableNow,
    // Solo si hay una ventana de lectura gratis abierta ahora: cuándo cierra.
    DateTime? FreeUntil,
    IReadOnlyList<WorkBuyLinkDto> BuyLinks,
    // true = la obra se abre para los miembros del club (ReadingAccess "members").
    bool ForMembers,
    // Portada de la edición en inglés (null = usar CoverUrl).
    string? CoverUrlEn = null
);

/// <summary>Una obra publicada para el sitemap de la web: dónde vive y cuándo cambió.</summary>
// Languages: idiomas en que se puede leer ("es" siempre; otros si hay capítulos traducidos y publicados).
public record SitemapWorkDto(string AffiliateSlug, string WorkSlug, DateTime LastModified, IReadOnlyList<string>? Languages = null);

// MembersOnly: cerrado ahora, pero un miembro del club lo puede leer.
// Translated: con ?lang, este capítulo tiene traducción publicada en ese idioma (Title ya viene traducido).
public record PublicWorkChapterRefDto(int Number, string Title, int WordCount, bool Readable, bool MembersOnly, bool Translated = false);

public record PublicWorkDto(
    PublicWorkSummaryDto Work,
    string? Isbn,
    string? ContentNotice,
    IReadOnlyList<PublicWorkChapterRefDto> Chapters,
    // Último capítulo que abrió este lector (solo con sesión de lector).
    int? ContinueAt,
    // Idioma en que viene el índice ("es" si no se pidió otro o no hay traducción publicada).
    string Language = "es",
    // Idiomas en que se puede leer esta obra ("es" + los que tienen algún capítulo traducido y publicado).
    IReadOnlyList<string>? Languages = null,
    // Crédito de la traducción en ese idioma (null = la web pone el texto por defecto).
    string? TranslationCredit = null
);

/// <summary>Un capítulo legible. Solo existe si el servidor decidió que se puede leer ahora.</summary>
public record PublicWorkChapterDto(
    string WorkSlug,
    string WorkTitle,
    int Number,
    string Title,
    string? ContentHtml,
    int WordCount,
    int? PrevNumber,
    int? NextNumber,
    string? ContentNotice,
    // Idioma del texto que viene ("es" = el original).
    string Language = "es",
    // Idiomas en que se puede leer este capítulo.
    IReadOnlyList<string>? Languages = null,
    string? TranslationCredit = null,
    // Título original, cuando el texto viene traducido.
    string? OriginalTitle = null
);
