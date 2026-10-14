using Microsoft.Extensions.Options;
using Notification.Api.Domain;
using Notification.Api.Messaging;
using TelecomPlatform.Contracts;
using TelecomPlatform.Messaging;

namespace Notification.Api.Consumers;

/// <summary>Turns quota events from Kafka into per-channel work items on RabbitMQ.</summary>
public sealed class QuotaThresholdConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<QuotaThresholdConsumer> logger)
    : KafkaConsumerService<QuotaThresholdReached>(scopeFactory, options, logger)
{
    protected override string Topic => Topics.QuotaThreshold;

    protected override async Task HandleAsync(QuotaThresholdReached message, IServiceProvider services, CancellationToken ct)
    {
        var queue = services.GetRequiredService<NotificationQueue>();
        foreach (var command in NotificationTemplates.For(message))
        {
            // If this throws halfway, the Kafka message is retried and some commands are enqueued twice;
            // the dispatcher's (EventId, Channel) uniqueness check makes that harmless.
            await queue.EnqueueAsync(command, attempt: 1, ct);
        }
    }
}
