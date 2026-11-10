using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

public interface IQuoteService
{
    /// <summary>Cotización de un negocio con lo que tiene hoy: módulos, vehículos, usuarios, clientes, país.</summary>
    Task<QuoteResultDto> ComputeForAffiliateAsync(Guid affiliateId);

    /// <summary>Cotización de un prospecto sin espacio: módulos y cantidades a mano.</summary>
    Task<QuoteResultDto> ComputeForSelectionAsync(QuoteSelectionRequest request);

    /// <summary>Libro de precios de /servicios con el nivel del país aplicado. Cacheado.</summary>
    Task<PublicPriceBookDto> GetPublicPriceBookAsync(string? country);

    /// <summary>Precio del botón "Mejorar" de un espacio (por el país del negocio).</summary>
    Task<UpgradePriceDto> GetUpgradePriceAsync(Guid affiliateId);

    Task<PricingConfigDto> GetConfigAsync();
    Task UpdateModuleWeightsAsync(UpdateModuleWeightsRequest request, string? adminEmail);
    Task UpdateTiersAsync(UpdateTiersRequest request, string? adminEmail);
    Task UpdateCountriesAsync(UpdateCountriesRequest request, string? adminEmail);
    Task UpdateSettingsAsync(PricingSettingsDto request, string? adminEmail);

    /// <summary>Marca un negocio como corporativo y fija sus locales. False si no existe.</summary>
    Task<bool> SetCorporateAsync(Guid affiliateId, SetCorporateRequest request);

    /// <summary>Nivel de precio (1, 2, 3…) de un país. Sin fila = 1.</summary>
    Task<int> GetTierForCountryAsync(string? country);
}
