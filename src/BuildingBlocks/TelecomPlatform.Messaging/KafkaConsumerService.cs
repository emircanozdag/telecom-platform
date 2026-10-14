using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TelecomPlatform.Messaging;

/// <summary>
/// Consumes a topic with at-least-once delivery: offsets are committed only after the handler succeeds,
/// so handlers must be idempotent. Each message is handled in its own DI scope.
/// </summary>
public abstract class KafkaConsumerService<TEvent> : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger _logger;

    protected KafkaConsumerService(IServiceScopeFactory scopeFactory, IOptions<KafkaOptions> options, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected abstract string Topic { get; }

    protected abstract Task HandleAsync(TEvent message, IServiceProvider services, CancellationToken ct);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        // Confluent's Consume() blocks, so run the loop off the host's startup thread.
        Task.Factory.StartNew(() => ConsumeLoopAsync(stoppingToken), stoppingToken,
            TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

    private async Task ConsumeLoopAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(Topic);
        _logger.LogInformation("Subscribed to {Topic} as group {Group}", Topic, _options.ConsumerGroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result;
                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException ex) when (!ex.Error.IsFatal)
                {
                    _logger.LogWarning(ex, "Transient consume error on {Topic}", Topic);
                    await Task.Delay(RetryDelay, stoppingToken);
                    continue;
                }

                await HandleWithRetryAsync(result, stoppingToken);
                consumer.Commit(result);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task HandleWithRetryAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        TEvent? message;
        try
        {
            message = JsonSerializer.Deserialize<TEvent>(result.Message.Value, JsonDefaults.Options);
        }
        catch (JsonException ex)
        {
            // A malformed message will never succeed; skip it instead of blocking the partition.
            _logger.LogError(ex, "Skipping malformed message at {TopicPartitionOffset}", result.TopicPartitionOffset);
            return;
        }

        if (message is null)
        {
            return;
        }

        // Retry until it succeeds: dropping a usage event would lose revenue.
        while (true)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await HandleAsync(message, scope.ServiceProvider, ct);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Handler failed for {TopicPartitionOffset}; retrying in {Delay}",
                    result.TopicPartitionOffset, RetryDelay);
                await Task.Delay(RetryDelay, ct);
            }
        }
    }
}
