using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IPlatformAddOnService
{
    /// <summary>Extras activos, ordenados — lo que muestra /servicios. Cacheado 10 min.</summary>
    Task<IReadOnlyList<PlatformAddOnPublicDto>> GetPublicAsync();

    /// <summary>Todos los extras, activos e inactivos — para /ops/precios.</summary>
    Task<IReadOnlyList<PlatformAddOnAdminDto>> GetAllAsync();

    Task<PlatformAddOnAdminDto> CreateAsync(UpsertPlatformAddOnRequest request, string? adminEmail);

    /// <summary>Null si no existe un extra con esa key.</summary>
    Task<PlatformAddOnAdminDto?> UpdateAsync(string key, UpsertPlatformAddOnRequest request, string? adminEmail);
}
