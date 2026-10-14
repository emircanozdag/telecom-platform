using Microsoft.Extensions.Options;
using Notification.Api.Domain;

namespace Notification.Api.Senders;

public sealed class SenderOptions
{
    public const string SectionName = "Senders";

    /// <summary>Probability (0-1) that a send fails. Use it to watch retries and dead-lettering locally.</summary>
    public double SimulatedFailureRate { get; set; }
}

/// <summary>
/// Stand-in for real SMS/email gateways: logs instead of sending. Swap in a real provider
/// by registering another <see cref="INotificationSender"/> for the channel.
/// </summary>
public sealed class LoggingSender(NotificationChannel channel, IOptions<SenderOptions> options, ILogger<LoggingSender> logger)
    : INotificationSender
{
    public NotificationChannel Channel { get; } = channel;

    public Task SendAsync(string recipient, string message, CancellationToken ct)
    {
        if (Random.Shared.NextDouble() < options.Value.SimulatedFailureRate)
        {
            throw new InvalidOperationException($"Simulated {Channel} gateway failure.");
        }

        logger.LogInformation("[{Channel}] to {Recipient}: {Message}", Channel, recipient, message);
        return Task.CompletedTask;
    }
}
