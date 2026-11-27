namespace Maalca.Application.Common.DTOs;

// Traducción de obras (WorkTranslation.cs). Panel del autor: /space/[slug]/works → "Traducción".

public record WorkTranslationSettingsDto(string Language, string? Brief, string? Glossary, string? Credit);

public record UpsertWorkTranslationSettingsRequest(string? Brief, string? Glossary, string? Credit);

/// <summary>Una fila del índice: el capítulo original y cómo va su traducción.</summary>
public record WorkChapterTranslationRefDto(
    Guid ChapterId,
    int Number,
    string SourceTitle,
    int SourceWordCount,
    // null = nunca se pidió traducción de este capítulo.
    string? Status,
    string? Title,
    int WordCount,
    // El original cambió después de traducirlo o revisarlo.
    bool Stale,
    bool Reviewed,
    string? Error,
    decimal CostUsd,
    DateTime? UpdatedAt
);

public record WorkTranslationOverviewDto(
    Guid WorkId,
    string WorkTitle,
    WorkTranslationSettingsDto Settings,
    IReadOnlyList<WorkChapterTranslationRefDto> Chapters,
    // Modelo que hará los borradores (registro AgentModels, nivel premium). null = no hay uno activo.
    string? ModelId,
    // Hay llave del proveedor configurada (en /ops o ANTHROPIC_API_KEY). Sin ella, la cola no avanza.
    bool Ready,
    decimal TotalCostUsd
);

/// <summary>
/// Pedir borradores. ChapterIds null = todos los que faltan (sin traducción, fallidos, o borradores sin
/// revisar cuyo original cambió). Force = rehacer también los revisados o publicados (pierde lo editado).
/// </summary>
public record QueueWorkTranslationRequest(IReadOnlyList<Guid>? ChapterIds, bool Force = false);

public record QueueWorkTranslationResultDto(int Queued, int Skipped);

/// <summary>Lado a lado: el original y su traducción, para revisar.</summary>
public record WorkChapterTranslationDetailDto(
    Guid ChapterId,
    int Number,
    string SourceTitle,
    string? SourceHtml,
    string? Status,
    string? Title,
    string? ContentHtml,
    bool Stale,
    bool Reviewed,
    string? Error,
    string? ModelId,
    decimal CostUsd,
    DateTime? GeneratedAt,
    DateTime? ReviewedAt,
    DateTime? PublishedAt
);

/// <summary>Guardar lo revisado. Marca la traducción como revisada contra el original actual.</summary>
public record UpdateWorkChapterTranslationRequest(string Title, string? ContentHtml);

public record PublishWorkChapterTranslationRequest(bool Published);
