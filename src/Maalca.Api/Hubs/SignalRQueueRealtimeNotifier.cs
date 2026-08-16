using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Microsoft.AspNetCore.SignalR;

namespace Maalca.Api.Hubs;

/// <summary>Implementación concreta de IQueueRealtimeNotifier sobre QueueHub — mismo patrón que
/// SignalROrderRealtimeNotifier/OrdersHub.</summary>
public class SignalRQueueRealtimeNotifier : IQueueRealtimeNotifier
{
    private readonly IHubContext<QueueHub> _hub;

    public SignalRQueueRealtimeNotifier(IHubContext<QueueHub> hub)
    {
        _hub = hub;
    }

    public async Task NotifyQueueUpdatedAsync(Guid affiliateId, List<QueueEntry> queue)
    {
        await _hub.Clients.Group(affiliateId.ToString()).SendAsync("QueueUpdated", queue);
    }
}
