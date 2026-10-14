namespace Subscriber.Api.Domain;

public sealed class Plan
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public long MonthlyQuotaMegabytes { get; init; }

    /// <summary>Overage price charged per megabyte beyond the quota.</summary>
    public decimal OveragePricePerMegabyte { get; init; }
}
