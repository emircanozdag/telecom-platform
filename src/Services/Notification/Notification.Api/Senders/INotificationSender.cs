using Notification.Api.Domain;

namespace Notification.Api.Senders;

public interface INotificationSender
{
    NotificationChannel Channel { get; }
    Task SendAsync(string recipient, string message, CancellationToken ct);
}
