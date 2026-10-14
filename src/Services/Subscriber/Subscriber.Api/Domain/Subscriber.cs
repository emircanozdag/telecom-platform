using TelecomPlatform.Contracts;

namespace Subscriber.Api.Domain;

public sealed class Subscriber
{
    internal static readonly int[] Thresholds = [80, 100];

    private Subscriber() { } // EF Core

    public Subscriber(string msisdn, string email, string fullName, string planCode, DateTime nowUtc)
    {
        Id = Guid.NewGuid();
        Msisdn = msisdn;
        Email = email;
        FullName = fullName;
        PlanCode = planCode;
        CurrentPeriod = BillingPeriod.For(nowUtc);
        CreatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }
    public string Msisdn { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public string PlanCode { get; private set; } = default!;
    public int CurrentPeriod { get; private set; }
    public long UsedMegabytesInPeriod { get; private set; }

    /// <summary>Highest threshold already announced this period, so each one fires only once.</summary>
    public int LastNotifiedThreshold { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Records data usage and returns the integration events it produces: always a <see cref="UsageRecorded"/>,
    /// plus a <see cref="QuotaThresholdReached"/> for each threshold newly crossed in this period.
    /// </summary>
    public IReadOnlyList<IntegrationEvent> RecordUsage(long megabytes, Plan plan, DateTime nowUtc)
    {
        if (megabytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(megabytes), "Usage must be positive.");
        }

        if (plan.Code != PlanCode)
        {
            throw new InvalidOperationException($"Plan {plan.Code} does not belong to subscriber {Id}.");
        }

        RollOverPeriodIfNeeded(nowUtc);

        // Only the part of this usage that exceeds the quota is billable as overage.
        var overageBefore = Math.Max(0, UsedMegabytesInPeriod - plan.MonthlyQuotaMegabytes);
        UsedMegabytesInPeriod += megabytes;
        var overageAfter = Math.Max(0, UsedMegabytesInPeriod - plan.MonthlyQuotaMegabytes);

        var events = new List<IntegrationEvent>
        {
            new UsageRecorded(Id, Msisdn, PlanCode, megabytes, overageAfter - overageBefore,
                plan.OveragePricePerMegabyte, CurrentPeriod),
        };

        var usedPercent = plan.MonthlyQuotaMegabytes == 0
            ? 100
            : UsedMegabytesInPeriod * 100 / plan.MonthlyQuotaMegabytes;

        // Announce only the highest newly crossed threshold, e.g. jumping from 50% to 120% sends just "100%".
        var crossed = Thresholds.Where(t => t > LastNotifiedThreshold && usedPercent >= t).DefaultIfEmpty(0).Max();
        if (crossed > 0)
        {
            LastNotifiedThreshold = crossed;
            events.Add(new QuotaThresholdReached(Id, Msisdn, Email, crossed,
                UsedMegabytesInPeriod, plan.MonthlyQuotaMegabytes));
        }

        return events;
    }

    private void RollOverPeriodIfNeeded(DateTime nowUtc)
    {
        var period = BillingPeriod.For(nowUtc);
        if (period == CurrentPeriod)
        {
            return;
        }

        CurrentPeriod = period;
        UsedMegabytesInPeriod = 0;
        LastNotifiedThreshold = 0;
    }
}
