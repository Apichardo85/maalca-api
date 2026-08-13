using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Maalca.Api.Hubs;

/// <summary>Implementación concreta de IOrderRealtimeNotifier sobre OrdersHub — vive acá porque
/// solo Api (no Application) puede referenciar SignalR/Hub directamente.</summary>
public class SignalROrderRealtimeNotifier : IOrderRealtimeNotifier
{
    private readonly IHubContext<OrdersHub> _hub;

    public SignalROrderRealtimeNotifier(IHubContext<OrdersHub> hub)
    {
        _hub = hub;
    }

    public async Task NotifyOrderUpdatedAsync(Guid affiliateId, OrderDto order)
    {
        await _hub.Clients.Group(affiliateId.ToString()).SendAsync("OrderUpdated", order);
    }
}
