namespace TelecomPlatform.Contracts;

/// <summary>Base shape for every event published to Kafka.</summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// A subscriber consumed mobile data. <paramref name="BillableMegabytes"/> is the part of this usage that
/// exceeded the plan quota and is charged at <paramref name="OveragePricePerMegabyte"/>.
/// </summary>
public sealed record UsageRecorded(
    Guid SubscriberId,
    string Msisdn,
    string PlanCode,
    long UsedMegabytes,
    long BillableMegabytes,
    decimal OveragePricePerMegabyte,
    int Period) : IntegrationEvent;

/// <summary>A subscriber crossed a quota threshold (80% or 100%) in the current period.</summary>
public sealed record QuotaThresholdReached(
    Guid SubscriberId,
    string Msisdn,
    string Email,
    int ThresholdPercent,
    long UsedMegabytes,
    long QuotaMegabytes) : IntegrationEvent;

public static class Topics
{
    public const string Usage = "telecom.usage.recorded";
    public const string QuotaThreshold = "telecom.quota.threshold-reached";
}
