using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IScreenWallService
{
    Task<List<ScreenWallDto>> GetAllAsync(Guid affiliateId);
    Task<ScreenWallDto> CreateAsync(Guid affiliateId, CreateScreenWallRequest request);
    Task<ScreenWallDto> UpdateAsync(Guid affiliateId, Guid wallId, UpdateScreenWallRequest request);
    Task<ScreenWallDto> UpdateSlotsAsync(Guid affiliateId, Guid wallId, UpdateScreenWallSlotsRequest request);
    Task DeleteAsync(Guid affiliateId, Guid wallId);
}
