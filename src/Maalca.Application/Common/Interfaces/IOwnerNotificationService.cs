namespace Maalca.Application.Common.Interfaces;

public record OwnerNotificationDto(
    Guid Id,
    string Type,
    string Title,
    string? Body,
    string TitleEn,
    string? BodyEn,
    string? Url,
    Guid? EntityId,
    DateTime CreatedAt,
    bool Read);

public record OwnerNotificationSummaryDto(int Unread, IReadOnlyDictionary<string, int> UnreadByType);

public record PushSubscribeRequest(string Endpoint, string P256dh, string Auth, string? Lang);

public record MarkNotificationsReadRequest(List<Guid>? Ids, string? Type);

public record PushUnsubscribeRequest(string? Endpoint);

/// <summary>
/// Avisos para el dueño del negocio: se crean desde los *NotificationService existentes en el
/// momento del evento, alimentan los badges/campana de /space y disparan Web Push a los
/// dispositivos suscritos. Best-effort: un aviso que falla NUNCA debe tumbar el pedido, la cita o
/// el pago que lo originó.
/// </summary>
public interface IOwnerNotificationService
{
    /// <summary>Crea el aviso y manda el push. Nunca lanza.</summary>
    Task NotifyAsync(Guid affiliateId, string type, string title, string? body, string titleEn, string? bodyEn, string? url, Guid? entityId);

    Task<IReadOnlyList<OwnerNotificationDto>> ListAsync(Guid affiliateId, int take);
    Task<OwnerNotificationSummaryDto> SummaryAsync(Guid affiliateId);

    /// <summary>Marca como leídos los ids dados, o todos los de un tipo, o todos si no se manda nada.</summary>
    Task MarkReadAsync(Guid affiliateId, MarkNotificationsReadRequest request);

    Task SubscribeAsync(Guid affiliateId, PushSubscribeRequest request);
    Task UnsubscribeAsync(Guid affiliateId, string endpoint);
}
