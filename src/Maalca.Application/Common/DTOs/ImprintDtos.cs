namespace Maalca.Application.Common.DTOs;

// ── Sello editorial (Affiliate tipo Publisher) y sus contratos con autores (ImprintAuthor) ────────

/// <summary>Un contrato visto desde cualquiera de los dos lados. WorkCount = obras del autor que salen con el sello.</summary>
public record ImprintContractDto(
    Guid Id,
    Guid ImprintId,
    string ImprintName,
    string? ImprintSlug,
    string? ImprintLogoUrl,
    Guid AuthorId,
    string AuthorName,
    string? AuthorSlug,
    string Status,
    decimal RoyaltyEbookPercent,
    decimal RoyaltyPrintPercent,
    IReadOnlyList<string> Rights,
    DateTime? StartsOn,
    DateTime? EndsOn,
    string? Notes,
    DateTime CreatedAt,
    DateTime? RespondedAt,
    DateTime? EndedAt,
    int WorkCount);

/// <summary>El sello invita a un autor por la dirección de su espacio (maalca.com/{slug}); se guarda su llave.</summary>
public record InviteImprintAuthorRequest(
    string AuthorSlug,
    decimal RoyaltyEbookPercent,
    decimal RoyaltyPrintPercent,
    IReadOnlyList<string>? Rights,
    DateTime? StartsOn,
    DateTime? EndsOn,
    string? Notes);

/// <summary>Cambiar los términos. Solo mientras está en invitación (o rechazado: vuelve a invitación).</summary>
public record UpdateImprintContractRequest(
    decimal RoyaltyEbookPercent,
    decimal RoyaltyPrintPercent,
    IReadOnlyList<string>? Rights,
    DateTime? StartsOn,
    DateTime? EndsOn,
    string? Notes);

public record RespondImprintContractRequest(bool Accept);

/// <summary>Terminar: cuántas obras dejaron de salir con el sello.</summary>
public record EndImprintContractResultDto(ImprintContractDto Contract, int WorksUnlinked);

/// <summary>Un sello que el autor puede elegir en la ficha de una obra.</summary>
public record ImprintOptionDto(Guid Id, string Name, string? Slug);

/// <summary>Una obra en el panel del editor (de cualquier autor del sello, o del sello mismo).</summary>
public record ImprintWorkDto(
    Guid Id,
    Guid AuthorId,
    string AuthorName,
    string? AuthorSlug,
    string Title,
    string? Subtitle,
    string Slug,
    string Kind,
    string Status,
    string? CoverUrl,
    string? CoverUrlEn,
    int? Year,
    string? Isbn,
    bool HasSynopsis,
    int ChapterCount,
    int WordCount,
    // Idiomas con al menos un capítulo traducido y publicado (además del original).
    IReadOnlyList<string> Translations,
    DateTime? UpdatedAt);
