using Notification.Api.Domain;
using RabbitMQ.Client;

namespace Notification.Api.Messaging;

/// <summary>
/// One durable work queue per channel, each with a dead-letter queue:
///   notifications (direct) --sms--> notifications.sms --(rejected)--> notifications.dlx --sms--> notifications.sms.dlq
/// </summary>
public static class NotificationTopology
{
    public const string Exchange = "notifications";
    public const string DeadLetterExchange = "notifications.dlx";
    public const string AttemptHeader = "x-attempt";

    public static string RoutingKey(NotificationChannel channel) => channel.ToString().ToLowerInvariant();
    public static string Queue(NotificationChannel channel) => $"{Exchange}.{RoutingKey(channel)}";
    public static string DeadLetterQueue(NotificationChannel channel) => $"{Queue(channel)}.dlq";

    public static void Declare(IModel channel)
    {
        channel.ExchangeDeclare(Exchange, ExchangeType.Direct, durable: true);
        channel.ExchangeDeclare(DeadLetterExchange, ExchangeType.Direct, durable: true);

        foreach (var notificationChannel in Enum.GetValues<NotificationChannel>())
        {
            var routingKey = RoutingKey(notificationChannel);

            channel.QueueDeclare(DeadLetterQueue(notificationChannel), durable: true, exclusive: false, autoDelete: false);
            channel.QueueBind(DeadLetterQueue(notificationChannel), DeadLetterExchange, routingKey);

            channel.QueueDeclare(Queue(notificationChannel), durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object>
                {
                    ["x-dead-letter-exchange"] = DeadLetterExchange,
                    ["x-dead-letter-routing-key"] = routingKey,
                });
            channel.QueueBind(Queue(notificationChannel), Exchange, routingKey);
        }
    }
}
