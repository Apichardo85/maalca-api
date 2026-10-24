using Maalca.Application.Common.DTOs;

namespace Maalca.Application.Common.Interfaces;

/// Monitoreo de pantallas: señal de vida de cada TV, estado para el panel y /ops, y comandos a distancia.
public interface IScreenDeviceService
{
    /// null = negocio inexistente (404). Siempre responde rápido: es la ruta caliente de todas las TVs.
    Task<ScreenHeartbeatResponse?> HeartbeatAsync(string slug, ScreenHeartbeatRequest request);
    Task<List<ScreenDeviceDto>> GetForAffiliateAsync(Guid affiliateId);
    Task<List<ScreenDeviceDto>> GetAllAsync();
    Task<bool> SetCommandAsync(Guid affiliateId, Guid deviceRowId, string command);
    Task<bool> SetArchivedAsync(Guid affiliateId, Guid deviceRowId, bool archived);
}
