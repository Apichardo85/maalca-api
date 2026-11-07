using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>Contratos recurrentes por cliente. ArgumentException = dato inválido; InvalidOperationException = conflicto
/// (ej. el cliente ya tiene un contrato activo). Nunca se borran: se desactivan.</summary>
public interface IContractService
{
    Task<IReadOnlyList<ContractDto>> GetAllAsync(Guid affiliateId, Guid? customerId, bool? active);
    Task<ContractDto?> GetAsync(Guid affiliateId, Guid contractId);
    Task<ContractDto> CreateAsync(Guid affiliateId, UpsertContractRequest request, string? actorId);
    Task<ContractDto?> UpdateAsync(Guid affiliateId, Guid contractId, UpsertContractRequest request, string? actorId);
    Task<ContractDto?> DeactivateAsync(Guid affiliateId, Guid contractId, string? actorId);
}
