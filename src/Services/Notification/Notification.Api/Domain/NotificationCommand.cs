namespace Notification.Api.Domain;

/// <summary>Work item placed on a RabbitMQ queue: "send this message on this channel".</summary>
public sealed record NotificationCommand(
    Guid EventId,
    Guid SubscriberId,
    NotificationChannel Channel,
    string Recipient,
    string Message);
