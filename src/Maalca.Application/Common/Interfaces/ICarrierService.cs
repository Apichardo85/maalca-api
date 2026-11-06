using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>Etiquetas de envío con el agregador del negocio (United Bridge): cuenta, tarifa real y compra.</summary>
public interface ICarrierService
{
    Task<CarrierAccountDto> GetAccountAsync(Guid affiliateId);
    Task<CarrierAccountDto> UpsertAccountAsync(Guid affiliateId, UpsertCarrierAccountRequest request);
    Task<LabelQuoteDto?> QuoteAsync(Guid affiliateId, Guid orderId, decimal? weightOz);
    Task<BuyLabelResultDto?> BuyLabelAsync(Guid affiliateId, Guid orderId, decimal? weightOz);
}
