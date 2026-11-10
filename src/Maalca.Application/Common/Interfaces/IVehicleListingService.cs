using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IVehicleListingService
{
    // Dashboard del dueño -- todos los vehículos, vendidos y ocultos incluidos.
    Task<List<VehicleListingDto>> GetAsync(Guid affiliateId);
    Task<VehicleListingDto> CreateAsync(Guid affiliateId, UpsertVehicleListingRequest request);
    Task<VehicleListingDto?> UpdateAsync(Guid affiliateId, Guid id, UpsertVehicleListingRequest request);
    Task<bool> DeleteAsync(Guid affiliateId, Guid id);

    // Configuración del dealer (Affiliate.Settings -> "dealer"): moneda, millaje, reservados.
    Task<DealerSettingsDto?> GetSettingsAsync(Guid affiliateId);
    Task<DealerSettingsDto?> UpdateSettingsAsync(Guid affiliateId, DealerSettingsDto request);

    // Vitrina pública. null = no existe, no está publicada o no es un negocio Dealer.
    Task<PublicVehiclesResponseDto?> GetPublicAsync(string slug);
}
