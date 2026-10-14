using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notification.Api.Data;
using Notification.Api.Domain;
using Notification.Api.Messaging;
using Notification.Api.Senders;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TelecomPlatform.Messaging;

namespace Notification.Api.Consumers;

/// <summary>
/// Works the per-channel RabbitMQ queues. A failed send is re-published with an incremented attempt
/// counter; after <see cref="RabbitMqOptions.MaxAttempts"/> it is rejected and lands in the DLQ.
/// </summary>
public sealed class NotificationDispatcher(
    RabbitMqConnection connection,
    NotificationQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<NotificationDispatcher> logger) : BackgroundService
{
    private readonly List<IModel> _channels = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var conn = await connection.GetAsync(stoppingToken);

        foreach (var notificationChannel in Enum.GetValues<NotificationChannel>())
        {
            var channel = conn.CreateModel();
            NotificationTopology.Declare(channel);
            channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += (_, delivery) => OnReceivedAsync(channel, delivery, stoppingToken);
            channel.BasicConsume(NotificationTopology.Queue(notificationChannel), autoAck: false, consumer);

            _channels.Add(channel);
            logger.LogInformation("Consuming {Queue}", NotificationTopology.Queue(notificationChannel));
        }

        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    private async Task OnReceivedAsync(IModel channel, BasicDeliverEventArgs delivery, CancellationToken ct)
    {
        var attempt = ReadAttempt(delivery.BasicProperties);
        NotificationCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<NotificationCommand>(delivery.Body.Span, JsonDefaults.Options);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Dead-lettering malformed message {DeliveryTag}", delivery.DeliveryTag);
            channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        if (command is null)
        {
            channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        try
        {
            await DeliverAsync(command, attempt, ct);
            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (attempt >= options.Value.MaxAttempts)
            {
                logger.LogError(ex, "Giving up on {Channel} notification for event {EventId} after {Attempt} attempts",
                    command.Channel, command.EventId, attempt);
                channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: false); // -> DLQ
                return;
            }

            logger.LogWarning(ex, "{Channel} notification for event {EventId} failed (attempt {Attempt}); retrying",
                command.Channel, command.EventId, attempt);
            await queue.EnqueueAsync(command, attempt + 1, ct);
            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
    }

    private async Task DeliverAsync(NotificationCommand command, int attempt, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        var alreadySent = await db.Notifications
            .AnyAsync(n => n.EventId == command.EventId && n.Channel == command.Channel, ct);
        if (alreadySent)
        {
            logger.LogDebug("Duplicate {Channel} notification for event {EventId} ignored", command.Channel, command.EventId);
            return;
        }

        var sender = scope.ServiceProvider.GetServices<INotificationSender>().Single(s => s.Channel == command.Channel);
        await sender.SendAsync(command.Recipient, command.Message, ct);

        db.Notifications.Add(new Domain.Notification
        {
            EventId = command.EventId,
            SubscriberId = command.SubscriberId,
            Channel = command.Channel,
            Recipient = command.Recipient,
            Message = command.Message,
            Attempts = attempt,
            SentAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    private static int ReadAttempt(IBasicProperties properties) =>
        properties.Headers is not null
        && properties.Headers.TryGetValue(NotificationTopology.AttemptHeader, out var value)
        && value is int attempt
            ? attempt
            : 1;

    public override void Dispose()
    {
        foreach (var channel in _channels)
        {
            channel.Dispose();
        }

        base.Dispose();
    }
}
