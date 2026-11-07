using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>Envases retornables, anaqueles con QR y movimientos. ArgumentException = dato inválido. Nada se borra:
/// los tipos se desactivan y los movimientos se corrigen con un "adjustment".</summary>
public interface IContainerService
{
    Task<IReadOnlyList<ContainerTypeDto>> GetTypesAsync(Guid affiliateId, bool includeInactive);
    Task<ContainerTypeDto> CreateTypeAsync(Guid affiliateId, UpsertContainerTypeRequest request, string? actorId);
    Task<ContainerTypeDto?> UpdateTypeAsync(Guid affiliateId, Guid typeId, UpsertContainerTypeRequest request, string? actorId);

    Task<IReadOnlyList<AnaquelDto>> GetAnaquelesAsync(Guid affiliateId, Guid? customerId, string? status);
    Task<AnaquelDto> CreateAnaquelAsync(Guid affiliateId, UpsertAnaquelRequest request, string? actorId);
    Task<AnaquelDto?> UpdateAnaquelAsync(Guid affiliateId, Guid anaquelId, UpsertAnaquelRequest request, string? actorId);
    Task<AnaquelScanDto?> ResolveQrAsync(Guid affiliateId, string qrCode);

    Task<RegisterContainerMovementResultDto> RegisterMovementAsync(Guid affiliateId, RegisterContainerMovementRequest request, string? actorId, string? actorName);
    Task<CustomerContainerBalanceDto?> GetBalanceAsync(Guid affiliateId, Guid customerId);
    Task<IReadOnlyList<ContainerMovementDto>> GetMovementsAsync(Guid affiliateId, Guid customerId, int take);
}
