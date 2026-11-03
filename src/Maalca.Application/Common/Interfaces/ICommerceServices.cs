using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IPriceListService
{
    Task<IReadOnlyList<PriceListDto>> GetAllAsync(Guid affiliateId);
    Task<PriceListDto> CreateAsync(Guid affiliateId, UpsertPriceListRequest request);
    Task<PriceListDto?> UpdateAsync(Guid affiliateId, Guid id, UpsertPriceListRequest request);
    Task<bool> DeleteAsync(Guid affiliateId, Guid id);
    /// <summary>Precio público vs precio con la lista para cada producto activo del negocio (mismo motor que el pedido).</summary>
    Task<IReadOnlyList<PriceListPreviewLineDto>?> PreviewAsync(Guid affiliateId, Guid id);
}

public interface IShippingService
{
    Task<IReadOnlyList<ShippingMethodDto>> GetMethodsAsync(Guid affiliateId);
    Task<ShippingMethodDto> CreateAsync(Guid affiliateId, UpsertShippingMethodRequest request);
    Task<ShippingMethodDto?> UpdateAsync(Guid affiliateId, Guid id, UpsertShippingMethodRequest request);
    Task<bool> DeleteAsync(Guid affiliateId, Guid id);
}
