namespace Maalca.Application.Common.DTOs;

/// <summary>Algo de la revisión previa a exportar. Level "error" bloquea; "warning" deja exportar.</summary>
public record WorkExportIssueDto(string Level, string Code, string Message);

/// <summary>Revisión previa de una edición (un idioma): qué falta y con qué datos va a salir el libro.</summary>
public record WorkExportPreflightDto(
    string Language,
    // Idiomas que se pueden exportar ("es" + los que tienen capítulos traducidos y publicados).
    IReadOnlyList<string> Languages,
    bool Ready,
    IReadOnlyList<WorkExportIssueDto> Issues,
    string Title,
    string Author,
    string? Publisher,
    string? Isbn,
    int ChapterCount,
    int WordCount);

public record WorkExportFileDto(byte[] Bytes, string FileName, string ContentType);
