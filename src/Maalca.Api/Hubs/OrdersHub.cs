using Microsoft.AspNetCore.SignalR;

namespace Maalca.Api.Hubs;

/// <summary>
/// Push en tiempo real para el Kitchen Display (/space/{slug}/kitchen). Mismo patrón que
/// QueueHub: un grupo por afiliado, el cliente se une con su propio Id al conectar. El servidor
/// nunca escucha comandos del cliente acá — solo empuja "OrderUpdated" desde OrderService cuando
/// un pedido se confirma (Paid) o cambia de estado (Preparing/Fulfilled/Canceled).
/// </summary>
public class OrdersHub : Hub
{
    public async Task JoinAffiliateGroup(string affiliateId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, affiliateId);
    }

    public async Task LeaveAffiliateGroup(string affiliateId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, affiliateId);
    }
}
