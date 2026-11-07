using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>Estado de cuenta, saldo vivo y antigüedad de saldos (solo lectura). Los pagos se registran en otra tarea.</summary>
public interface IBillingService
{
    /// <summary>Null si el cliente no existe o no es de este negocio. asOf = null → ahora (UTC).</summary>
    Task<CustomerStatementDto?> GetStatementAsync(Guid affiliateId, Guid customerId, DateTime? asOf = null);

    Task<AgingReportDto> GetAgingAsync(Guid affiliateId, DateTime? asOf = null);
}
