namespace Maalca.Domain.Entities;

/// <summary>
/// Evento de entrega/apertura/clic de un correo enviado por Resend (webhook email.*).
/// maalca-web verifica la firma Svix y reenvía acá; SvixId garantiza idempotencia ante reintentos.
/// </summary>
public class EmailEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SvixId { get; set; } = string.Empty;
    /// <summary>email.delivered, email.opened, email.clicked, email.bounced, ...</summary>
    public string EventType { get; set; } = string.Empty;
    public string? ResendEmailId { get; set; }
    public string? ToEmail { get; set; }
    public string? Subject { get; set; }
    public string? ClickedUrl { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
