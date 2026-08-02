namespace Maalca.Domain.Entities;

// Idempotency guard for the Stripe webhook — Stripe may redeliver the same
// event, and each EventId must only be processed once.
public class StripeProcessedEvent
{
    public string EventId { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}
