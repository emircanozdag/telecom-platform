namespace TelecomPlatform.Messaging;

public interface IEventPublisher
{
    /// <summary>Publishes a pre-serialized event. The key controls Kafka partitioning (ordering per key).</summary>
    Task PublishAsync(string topic, string key, string eventType, string payload, CancellationToken ct);
}
