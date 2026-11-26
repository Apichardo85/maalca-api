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
    DateTime? UpdatedAt
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
    int SortOrder
);

/// <summary>Number null en el alta = va al final.</summary>
public record UpsertWorkChapterRequest(int? Number, string Title, string? ContentHtml, bool IsSample);

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
    IReadOnlyList<WorkBuyLinkDto> BuyLinks
);

public record PublicWorkChapterRefDto(int Number, string Title, int WordCount, bool Readable);

public record PublicWorkDto(
    PublicWorkSummaryDto Work,
    string? Isbn,
    string? ContentNotice,
    IReadOnlyList<PublicWorkChapterRefDto> Chapters
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
    string? ContentNotice
);
