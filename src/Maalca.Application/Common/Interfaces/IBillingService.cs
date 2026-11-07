using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>Estado de cuenta, saldo vivo, antigüedad de saldos y registro de pagos/abonos.
/// ArgumentException = dato inválido; InvalidOperationException = conflicto (factura anulada o ya pagada).</summary>
public interface IBillingService
{
    /// <summary>Null si el cliente no existe o no es de este negocio. asOf = null → ahora (UTC).</summary>
    Task<CustomerStatementDto?> GetStatementAsync(Guid affiliateId, Guid customerId, DateTime? asOf = null);

    Task<AgingReportDto> GetAgingAsync(Guid affiliateId, DateTime? asOf = null);

    /// <summary>Registra un pago o abono. Idempotente por IdempotencyKey. Si va ligado a una factura y la completa, la pasa a Paid.</summary>
    Task<RegisterPaymentResultDto> RegisterPaymentAsync(Guid affiliateId, RegisterPaymentRequest request, string? actorId, string? actorName);

    Task<IReadOnlyList<PaymentDto>> GetPaymentsAsync(Guid affiliateId, Guid customerId, int take = 100);
}
