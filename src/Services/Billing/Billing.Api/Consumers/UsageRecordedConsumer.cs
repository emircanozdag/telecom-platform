using Microsoft.Extensions.Options;
using TelecomPlatform.Contracts;
using TelecomPlatform.Messaging;

namespace Billing.Api.Consumers;

public sealed class UsageRecordedConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<UsageRecordedConsumer> logger)
    : KafkaConsumerService<UsageRecorded>(scopeFactory, options, logger)
{
    protected override string Topic => Topics.Usage;

    protected override Task HandleAsync(UsageRecorded message, IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<UsageRecordedHandler>().HandleAsync(message, ct);
}
