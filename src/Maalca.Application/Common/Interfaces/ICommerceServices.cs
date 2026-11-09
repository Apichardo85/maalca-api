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
    /// <summary>Clientes del negocio y en qué lista está cada uno. listId filtra a los de esa lista; q busca por nombre o teléfono.</summary>
    Task<IReadOnlyList<PriceListCustomerDto>> GetCustomersAsync(Guid affiliateId, Guid? listId, string? q, int take);
    /// <summary>Asigna (o quita) la lista a varios clientes. Devuelve cuántos cambiaron; null si la lista no es del negocio.</summary>
    Task<int?> AssignCustomersAsync(Guid affiliateId, Guid listId, AssignPriceListCustomersRequest request);
    /// <summary>Cuántos clientes tiene cada lista (y cuántos quedan a precio público), para los contadores del panel.</summary>
    Task<IReadOnlyList<PriceListCustomerCountDto>> GetCustomerCountsAsync(Guid affiliateId);
}

public interface IShippingService
{
    Task<IReadOnlyList<ShippingMethodDto>> GetMethodsAsync(Guid affiliateId);
    Task<ShippingMethodDto> CreateAsync(Guid affiliateId, UpsertShippingMethodRequest request);
    Task<ShippingMethodDto?> UpdateAsync(Guid affiliateId, Guid id, UpsertShippingMethodRequest request);
    Task<bool> DeleteAsync(Guid affiliateId, Guid id);
}
