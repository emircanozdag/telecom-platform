using System.Text.Json;
using Subscriber.Api.Data;
using TelecomPlatform.Contracts;
using TelecomPlatform.Messaging;

namespace Subscriber.Api.Outbox;

public static class OutboxWriter
{
    /// <summary>Stages events in the outbox; they are persisted by the caller's SaveChanges.</summary>
    public static void Enqueue(SubscriberDbContext db, Guid subscriberId, IEnumerable<IntegrationEvent> events)
    {
        foreach (var @event in events)
        {
            var topic = @event switch
            {
                UsageRecorded => Topics.Usage,
                QuotaThresholdReached => Topics.QuotaThreshold,
                _ => throw new NotSupportedException($"No topic mapped for {@event.GetType().Name}."),
            };

            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = @event.EventId,
                Topic = topic,
                // Keying by subscriber keeps each subscriber's events ordered within one partition.
                Key = subscriberId.ToString(),
                EventType = @event.GetType().Name,
                Payload = JsonSerializer.Serialize(@event, @event.GetType(), JsonDefaults.Options),
                OccurredAtUtc = @event.OccurredAtUtc,
            });
        }
    }
}
