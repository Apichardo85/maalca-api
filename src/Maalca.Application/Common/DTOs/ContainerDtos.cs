namespace Maalca.Application.Common.DTOs;

public record ContainerTypeDto(Guid Id, string Name, decimal? CapacityLiters, decimal Deposit, bool IsActive);

public record UpsertContainerTypeRequest(string Name, decimal? CapacityLiters, decimal Deposit = 0, bool IsActive = true);

public record AnaquelDto(
    Guid Id,
    string QrCode,
    string? Label,
    string? Location,
    int Capacity,
    string Status,
    Guid? CustomerId,
    string? CustomerName,
    string? Notes);

/// <summary>CustomerId null = anaquel sin asignar (status "disponible"). El QR lo genera el servidor al crear.</summary>
public record UpsertAnaquelRequest(
    Guid? CustomerId,
    string? Label,
    string? Location,
    int Capacity = 0,
    string? Status = null,
    string? Notes = null);

public record ContainerBalanceItemDto(
    Guid ContainerTypeId,
    string ContainerTypeName,
    int Delivered,
    int Returned,
    int Damaged,
    int Adjustments,
    /// <summary>Envases que el cliente tiene en su poder: entregados − devueltos − dañados + ajustes. Negativo = devolvió de más (revisar).</summary>
    int Outstanding);

public record CustomerContainerBalanceDto(Guid CustomerId, string CustomerName, IReadOnlyList<ContainerBalanceItemDto> Items);

public record ContainerMovementDto(
    Guid Id,
    Guid CustomerId,
    Guid ContainerTypeId,
    string? ContainerTypeName,
    Guid? AnaquelId,
    string Type,
    int Quantity,
    DateTime OccurredAt,
    string? RegisteredBy,
    string? Notes);

/// <summary>CustomerId puede omitirse si viene AnaquelId (se toma el cliente del anaquel).</summary>
public record RegisterContainerMovementRequest(
    Guid ContainerTypeId,
    string Type,
    int Quantity,
    Guid? CustomerId = null,
    Guid? AnaquelId = null,
    DateTime? OccurredAt = null,
    string? Notes = null,
    string? IdempotencyKey = null);

public record RegisterContainerMovementResultDto(ContainerMovementDto Movement, bool Duplicate, CustomerContainerBalanceDto Balance);

/// <summary>Lo que ve el conductor al escanear: el anaquel, su cliente y cuántos envases tiene en su poder.</summary>
public record AnaquelScanDto(AnaquelDto Anaquel, CustomerContainerBalanceDto? Balance);
