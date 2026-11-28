using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>
/// Exportar una obra como libro (EPUB 3 por ahora). Lo puede hacer el autor dueño o el sello con el que sale.
/// null = la obra no es de ese negocio ni sale con ese sello. ArgumentException = la revisión tiene errores (400).
/// </summary>
public interface IWorkExportService
{
    Task<WorkExportPreflightDto?> PreflightAsync(Guid affiliateId, Guid workId, string? language, CancellationToken ct = default);
    Task<WorkExportFileDto?> BuildEpubAsync(Guid affiliateId, Guid workId, string? language, CancellationToken ct = default);
}
