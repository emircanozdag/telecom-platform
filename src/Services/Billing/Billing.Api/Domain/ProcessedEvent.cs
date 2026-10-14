namespace Billing.Api.Domain;

/// <summary>Inbox record: makes the at-least-once Kafka consumer effectively exactly-once.</summary>
public sealed class ProcessedEvent
{
    public Guid EventId { get; init; }
    public DateTime ProcessedAtUtc { get; init; }
}
