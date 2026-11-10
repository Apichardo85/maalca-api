using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IVehicleLeadService
{
    // Vitrina pública: el cliente final deja su interés. KeyNotFoundException = el negocio no existe,
    // no está publicado o no es Dealer; ArgumentException = datos inválidos o formulario apagado.
    Task<PublicVehicleLeadResultDto> CreatePublicAsync(string slug, CreateVehicleLeadRequest request);

    // Dashboard del dueño.
    Task<List<VehicleLeadDto>> GetAsync(Guid affiliateId, string? status = null);
    Task<VehicleLeadDto?> UpdateAsync(Guid affiliateId, Guid id, UpdateVehicleLeadRequest request);
    Task<bool> DeleteAsync(Guid affiliateId, Guid id);
    Task<int> CountNewAsync(Guid affiliateId);
}
