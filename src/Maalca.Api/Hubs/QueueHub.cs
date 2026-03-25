using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Maalca.Api.Hubs;

[Authorize]
public class QueueHub : Hub
{
    public async Task JoinQueueGroup(string affiliateId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, affiliateId);
    }

    public async Task LeaveQueueGroup(string affiliateId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, affiliateId);
    }

    public async Task NotifyQueueUpdate(string affiliateId, object queueData)
    {
        await Clients.Group(affiliateId).SendAsync("QueueUpdated", queueData);
    }

    public async Task NotifyPositionChanged(string affiliateId, string connectionId, int newPosition)
    {
        await Clients.Client(connectionId).SendAsync("PositionChanged", newPosition);
    }

    public async Task NotifyCalled(string affiliateId, string connectionId, string message)
    {
        await Clients.Client(connectionId).SendAsync("Called", message);
    }
}
