using Maalca.Application.Common.DTOs;
using Maalca.Application.Creator;

namespace Maalca.Application.Common.Interfaces;

/// <summary>Obras de un negocio Creator (autor). Ver Work.cs.</summary>
public interface IWorkService
{
    // Dashboard del dueño -- borradores incluidos. ArgumentException = datos inválidos (400).
    Task<List<WorkDto>> GetAsync(Guid affiliateId);
    Task<WorkDetailDto?> GetDetailAsync(Guid affiliateId, Guid workId);
    Task<WorkDto> CreateAsync(Guid affiliateId, UpsertWorkRequest request);
    Task<WorkDto?> UpdateAsync(Guid affiliateId, Guid workId, UpsertWorkRequest request);
    Task<bool> DeleteAsync(Guid affiliateId, Guid workId);

    Task<WorkChapterDto?> GetChapterAsync(Guid affiliateId, Guid workId, Guid chapterId);
    Task<WorkChapterDto?> CreateChapterAsync(Guid affiliateId, Guid workId, UpsertWorkChapterRequest request);
    Task<WorkChapterDto?> UpdateChapterAsync(Guid affiliateId, Guid workId, Guid chapterId, UpsertWorkChapterRequest request);
    Task<bool> DeleteChapterAsync(Guid affiliateId, Guid workId, Guid chapterId);
    Task<IReadOnlyList<WorkChapterRefDto>?> ReorderChaptersAsync(Guid affiliateId, Guid workId, ReorderWorkChaptersRequest request);
    Task<IReadOnlyList<WorkChapterRefDto>?> ImportChaptersAsync(Guid affiliateId, Guid workId, ImportWorkChaptersRequest request);

    // Página pública. null = el autor no existe, no está publicado, no es Creator/Publisher,
    // o la obra/capítulo no existe, es borrador o no se puede leer ahora.
    // reader: lector con sesión (club). null = visitante; IsMember abre lo de ReadingAccess "members".
    Task<List<PublicWorkSummaryDto>?> GetPublicAsync(string affiliateSlug, ReaderContext? reader = null);
    Task<PublicWorkDto?> GetPublicWorkAsync(string affiliateSlug, string workSlug, ReaderContext? reader = null);
    Task<PublicWorkChapterDto?> GetPublicChapterAsync(string affiliateSlug, string workSlug, int number, ReaderContext? reader = null);
}
