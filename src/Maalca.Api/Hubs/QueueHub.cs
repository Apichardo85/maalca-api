using Microsoft.AspNetCore.SignalR;

namespace Maalca.Api.Hubs;

/// <summary>
/// Push en tiempo real para la fila de espera (/space/{slug}/queue). Mismo patrón que OrdersHub:
/// un grupo por afiliado, el cliente se une con su propio Id al conectar. El servidor nunca
/// escucha comandos del cliente acá — solo empuja "QueueUpdated" desde QueueService cuando
/// alguien entra a la fila o cambia de estado (ver SignalRQueueRealtimeNotifier).
/// </summary>
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
}
