using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Notification.Api.Messaging;

/// <summary>Single shared connection (connections are expensive; channels are cheap).</summary>
public sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnection> logger) : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var factory = new ConnectionFactory
            {
                Uri = new Uri(options.Value.Uri),
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true,
                ClientProvidedName = "notification-service",
            };

            // RabbitMQ may still be starting (e.g. docker compose up); retry with backoff.
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    _connection = factory.CreateConnection();
                    return _connection;
                }
                catch (Exception ex) when (attempt < 10)
                {
                    var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                    logger.LogWarning(ex, "RabbitMQ not reachable (attempt {Attempt}); retrying in {Delay}", attempt, delay);
                    await Task.Delay(delay, ct);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _connection?.Dispose();
}
