using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IScreenAdService
{
    /// <summary>Todos los comerciales del afiliado, activos e inactivos — para el panel de administración.</summary>
    Task<IReadOnlyList<ScreenAdDto>> GetAllAsync(Guid affiliateId);

    Task<ScreenAdDto> CreateAsync(Guid affiliateId, CreateScreenAdRequest request);

    Task<ScreenAdDto?> UpdateAsync(Guid affiliateId, Guid adId, UpdateScreenAdRequest request);

    Task<bool> DeleteAsync(Guid affiliateId, Guid adId);
}
