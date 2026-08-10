using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IPublicCatalogService
{
    Task<AffiliatePublicDto?> GetAffiliateBySlugAsync(string slug);
    Task<PublicCatalogResponse?> GetCatalogAsync(string slug, Guid? screenId = null);
    Task<List<FeaturedAffiliateDto>> GetFeaturedAffiliatesAsync();
}
