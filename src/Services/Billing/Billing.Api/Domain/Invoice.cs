namespace Billing.Api.Domain;

/// <summary>Running usage invoice for one subscriber in one calendar-month period.</summary>
public sealed class Invoice
{
    private Invoice() { } // EF Core

    public Invoice(Guid subscriberId, string msisdn, int period)
    {
        SubscriberId = subscriberId;
        Msisdn = msisdn;
        Period = period;
    }

    public Guid SubscriberId { get; private set; }
    public int Period { get; private set; }
    public string Msisdn { get; private set; } = default!;
    public long TotalMegabytes { get; private set; }
    public long OverageMegabytes { get; private set; }
    public decimal OverageAmount { get; private set; }
    public int UsageRecordCount { get; private set; }
    public DateTime? LastUsageAtUtc { get; private set; }

    public void AddUsage(long usedMegabytes, long billableMegabytes, decimal overagePricePerMegabyte, DateTime occurredAtUtc)
    {
        if (usedMegabytes <= 0 || billableMegabytes < 0 || billableMegabytes > usedMegabytes)
        {
            throw new ArgumentException("Billable megabytes must be between 0 and the used megabytes.");
        }

        TotalMegabytes += usedMegabytes;
        OverageMegabytes += billableMegabytes;
        // Round each charge to kuruş so the invoice equals the sum of its line items.
        OverageAmount += Math.Round(billableMegabytes * overagePricePerMegabyte, 2, MidpointRounding.AwayFromZero);
        UsageRecordCount++;

        if (LastUsageAtUtc is null || occurredAtUtc > LastUsageAtUtc)
        {
            LastUsageAtUtc = occurredAtUtc;
        }
    }
}
