using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface ICanalService
{
    Task<List<CanalDto>> GetCanalesAsync(Guid affiliateId);
    Task<CanalDto> CreateAsync(Guid affiliateId, CreateCanalRequest request);
    Task<CanalDto?> UpdateAsync(Guid affiliateId, Guid canalId, UpdateCanalRequest request);
    Task<bool> DeleteAsync(Guid affiliateId, Guid canalId);
}
