namespace Notification.Api.Domain;

public enum NotificationChannel
{
    Sms,
    Email,
}

/// <summary>A notification that was delivered. Unique per (EventId, Channel) so redeliveries don't resend.</summary>
public sealed class Notification
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid EventId { get; init; }
    public Guid SubscriberId { get; init; }
    public NotificationChannel Channel { get; init; }
    public required string Recipient { get; init; }
    public required string Message { get; init; }
    public int Attempts { get; init; }
    public DateTime SentAtUtc { get; init; }
}
