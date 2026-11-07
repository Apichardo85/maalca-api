using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>Factura las entregas de reparto por ciclo de cobro. ArgumentException = dato inválido.</summary>
public interface IDeliveryBillingService
{
    Task<UnbilledReportDto> GetUnbilledAsync(Guid affiliateId);
    Task<BillDeliveriesResultDto> BillAsync(Guid affiliateId, BillDeliveriesRequest request, string? actorId, string? actorName);
}
