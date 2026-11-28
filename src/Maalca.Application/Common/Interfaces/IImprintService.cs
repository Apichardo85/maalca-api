using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>
/// Sello editorial (ImprintAuthor.cs). El sello invita, el autor acepta; las obras se enlazan por llave.
/// null = no existe o no es de ese negocio (404). ArgumentException = datos inválidos (400).
/// InvalidOperationException = el negocio no es del tipo que pide la operación (403).
/// </summary>
public interface IImprintService
{
    // Lado del sello (el negocio activo es Publisher).
    Task<IReadOnlyList<ImprintContractDto>> GetAuthorsAsync(Guid imprintId);
    Task<ImprintContractDto> InviteAsync(Guid imprintId, InviteImprintAuthorRequest request);
    Task<ImprintContractDto?> UpdateTermsAsync(Guid imprintId, Guid contractId, UpdateImprintContractRequest request);
    Task<EndImprintContractResultDto?> EndAsync(Guid imprintId, Guid contractId);
    Task<IReadOnlyList<ImprintWorkDto>> GetWorksAsync(Guid imprintId);

    // Lado del autor.
    Task<IReadOnlyList<ImprintContractDto>> GetMyContractsAsync(Guid authorId);
    Task<ImprintContractDto?> RespondAsync(Guid authorId, Guid contractId, bool accept);
    Task<IReadOnlyList<ImprintOptionDto>> GetOptionsAsync(Guid affiliateId);

    /// <summary>Regla de enlace: ¿puede esta obra (de affiliateId) salir con imprintId?</summary>
    Task<bool> CanUseImprintAsync(Guid affiliateId, Guid imprintId);
}
