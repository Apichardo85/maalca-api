using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>
/// Traducción de obras del autor (WorkTranslation.cs). El modelo hace el borrador en segundo plano
/// (WorkTranslationWorker); el autor lo revisa lado a lado y decide qué se publica.
/// null = la obra no es de ese negocio. ArgumentException = datos inválidos (400).
/// </summary>
public interface IWorkTranslationService
{
    Task<WorkTranslationOverviewDto?> GetOverviewAsync(Guid affiliateId, Guid workId, string language);
    Task<WorkTranslationSettingsDto?> SaveSettingsAsync(Guid affiliateId, Guid workId, string language, UpsertWorkTranslationSettingsRequest request);
    Task<QueueWorkTranslationResultDto?> QueueAsync(Guid affiliateId, Guid workId, string language, QueueWorkTranslationRequest request);
    Task<WorkChapterTranslationDetailDto?> GetChapterAsync(Guid affiliateId, Guid workId, string language, Guid chapterId);
    Task<WorkChapterTranslationDetailDto?> UpdateChapterAsync(Guid affiliateId, Guid workId, string language, Guid chapterId, UpdateWorkChapterTranslationRequest request);
    Task<WorkChapterTranslationDetailDto?> SetPublishedAsync(Guid affiliateId, Guid workId, string language, Guid chapterId, bool published);

    /// <summary>Worker: toma un capítulo en cola y hace su borrador. Devuelve true si procesó uno.</summary>
    Task<bool> ProcessNextAsync(CancellationToken ct);
}
