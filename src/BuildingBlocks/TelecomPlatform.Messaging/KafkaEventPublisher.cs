using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace TelecomPlatform.Messaging;

public sealed class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaEventPublisher(IOptions<KafkaOptions> options)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            // Idempotent producer: no duplicates or reordering caused by producer retries.
            EnableIdempotence = true,
            Acks = Acks.All,
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync(string topic, string key, string eventType, string payload, CancellationToken ct)
    {
        var message = new Message<string, string>
        {
            Key = key,
            Value = payload,
            Headers = new Headers { { "event-type", Encoding.UTF8.GetBytes(eventType) } },
        };
        await _producer.ProduceAsync(topic, message, ct);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
