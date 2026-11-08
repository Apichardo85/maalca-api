using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IWaterQualityService
{
    Task<WaterQualitySummaryDto> GetSummaryAsync(Guid affiliateId);

    Task<IReadOnlyList<WaterQualityTestDto>> GetTestsAsync(Guid affiliateId, int? year);
    Task<WaterQualityTestDto> CreateTestAsync(Guid affiliateId, UpsertWaterQualityTestRequest request, string? actorId);
    Task<WaterQualityTestDto?> UpdateTestAsync(Guid affiliateId, Guid testId, UpsertWaterQualityTestRequest request, string? actorId);
    /// <summary>Desactiva el análisis. False si no existe.</summary>
    Task<bool> DeleteTestAsync(Guid affiliateId, Guid testId, string? actorId);

    Task<IReadOnlyList<WaterMaintenanceDto>> GetMaintenanceAsync(Guid affiliateId);
    Task<WaterMaintenanceDto> CreateMaintenanceAsync(Guid affiliateId, UpsertWaterMaintenanceRequest request, string? actorId);
    Task<WaterMaintenanceDto?> UpdateMaintenanceAsync(Guid affiliateId, Guid logId, UpsertWaterMaintenanceRequest request, string? actorId);
    Task<bool> DeleteMaintenanceAsync(Guid affiliateId, Guid logId, string? actorId);

    /// <summary>Datos públicos de calidad por slug. Null si el negocio no existe o está inactivo.</summary>
    Task<PublicWaterQualityDto?> GetPublicAsync(string slug);
}
