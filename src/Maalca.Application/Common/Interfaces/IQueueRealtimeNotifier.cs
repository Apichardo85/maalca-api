using Maalca.Domain.Entities;

namespace Maalca.Application.Common.Interfaces;

/// <summary>
/// Push en tiempo real para la fila de espera (/space/{slug}/queue) — mismo patrón exacto que
/// IOrderRealtimeNotifier para el Kitchen Display. La implementación concreta (que envuelve
/// IHubContext&lt;QueueHub&gt;) vive en Maalca.Api, ya que Application no puede depender de Api.
/// </summary>
public interface IQueueRealtimeNotifier
{
    /// <summary>Alguien entró a la fila o cambió de estado (llamado, atendido, no-show) —
    /// refresca la pantalla de fila de espera del negocio.</summary>
    Task NotifyQueueUpdatedAsync(Guid affiliateId, List<QueueEntry> queue);
}
