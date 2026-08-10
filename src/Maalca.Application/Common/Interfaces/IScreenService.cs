using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IScreenService
{
    Task<List<ScreenDto>> GetAllAsync(Guid affiliateId);
    Task<ScreenDto> CreateAsync(Guid affiliateId, CreateScreenRequest request);
    Task<ScreenDto> UpdateAsync(Guid affiliateId, Guid screenId, UpdateScreenRequest request);
    Task DeleteAsync(Guid affiliateId, Guid screenId);
}
