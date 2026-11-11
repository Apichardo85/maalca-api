using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// <summary>Conexión del negocio con su Clover (solo lectura) y resumen de ventas leído en vivo.</summary>
public interface ICloverService
{
    Task<CloverConnectionDto> GetConnectionAsync(Guid affiliateId);
    Task<CloverConnectionDto> ConnectAsync(Guid affiliateId, ConnectCloverRequest request);
    Task DisconnectAsync(Guid affiliateId);
    Task<CloverSalesReportDto> GetSalesReportAsync(Guid affiliateId, DateOnly from, DateOnly to);
}
