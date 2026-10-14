using Microsoft.EntityFrameworkCore;
using Subscriber.Api.Data;
using TelecomPlatform.Messaging;

namespace Subscriber.Api.Outbox;

/// <summary>Relays outbox rows to Kafka. Safe to run on several instances thanks to SKIP LOCKED.</summary>
public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IEventPublisher publisher,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var published = await PublishBatchAsync(stoppingToken);
                if (published == BatchSize)
                {
                    continue; // More may be waiting; skip the delay.
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox publishing failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    internal async Task<int> PublishBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SubscriberDbContext>();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Lock the batch so concurrent publishers pick different rows instead of double-publishing.
        var messages = await db.OutboxMessages
            .FromSql($"""
                SELECT * FROM "OutboxMessages"
                WHERE "ProcessedAtUtc" IS NULL
                ORDER BY "OccurredAtUtc"
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message.Topic, message.Key, message.EventType, message.Payload, ct);
                message.ProcessedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = ex.Message;
                logger.LogWarning(ex, "Failed to publish outbox message {MessageId}", message.Id);
                // Stop here to preserve per-subscriber ordering; the rest is retried next poll.
                break;
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return messages.Count(m => m.ProcessedAtUtc is not null);
    }
}
