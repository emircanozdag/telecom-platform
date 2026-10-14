using System.Text.Json;
using Notification.Api.Domain;
using RabbitMQ.Client;
using TelecomPlatform.Messaging;

namespace Notification.Api.Messaging;

/// <summary>Publishes notification commands with publisher confirms, so a command is never silently lost.</summary>
public sealed class NotificationQueue(RabbitMqConnection connection) : IDisposable
{
    private static readonly TimeSpan ConfirmTimeout = TimeSpan.FromSeconds(10);

    private readonly SemaphoreSlim _lock = new(1, 1); // IModel is not thread-safe.
    private IModel? _channel;

    public async Task EnqueueAsync(NotificationCommand command, int attempt, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var channel = await GetChannelAsync(ct);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.MessageId = $"{command.EventId}:{command.Channel}";
            properties.Headers = new Dictionary<string, object> { [NotificationTopology.AttemptHeader] = attempt };

            var body = JsonSerializer.SerializeToUtf8Bytes(command, JsonDefaults.Options);
            channel.BasicPublish(NotificationTopology.Exchange, NotificationTopology.RoutingKey(command.Channel),
                mandatory: true, properties, body);
            channel.WaitForConfirmsOrDie(ConfirmTimeout);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IModel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        var conn = await connection.GetAsync(ct);
        _channel = conn.CreateModel();
        _channel.ConfirmSelect();
        NotificationTopology.Declare(_channel);
        return _channel;
    }

    public void Dispose() => _channel?.Dispose();
}
