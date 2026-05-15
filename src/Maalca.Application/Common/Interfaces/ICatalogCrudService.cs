using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface ICatalogCrudService
{
    Task<List<CatalogItemDto>> GetItemsAsync(Guid affiliateId);
    Task<CatalogItemDto?> GetItemAsync(Guid affiliateId, Guid itemId);
    Task<CatalogItemDto> CreateItemAsync(Guid affiliateId, CreateCatalogItemRequest request);
    Task<CatalogItemDto?> UpdateItemAsync(Guid affiliateId, Guid itemId, UpdateCatalogItemRequest request);
    Task<bool> DeleteItemAsync(Guid affiliateId, Guid itemId);
}
