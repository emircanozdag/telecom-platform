namespace Subscriber.Api.Outbox;

/// <summary>
/// An event waiting to be published to Kafka. Written in the same transaction as the state change,
/// so an event is never lost and never published for a change that was rolled back.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public required string Topic { get; init; }
    public required string Key { get; init; }
    public required string EventType { get; init; }
    public required string Payload { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
