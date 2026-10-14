using Subscriber.Api.Domain;
using TelecomPlatform.Contracts;
using SubscriberEntity = Subscriber.Api.Domain.Subscriber;

namespace Subscriber.UnitTests;

public class SubscriberUsageTests
{
    private static readonly DateTime October = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Plan Plan = new()
    {
        Code = "TEST-1000",
        Name = "Test 1000 MB",
        MonthlyQuotaMegabytes = 1_000,
        OveragePricePerMegabyte = 0.05m,
    };

    private static SubscriberEntity NewSubscriber() =>
        new("905321234567", "user@example.com", "Test User", Plan.Code, October);

    [Fact]
    public void Usage_below_threshold_emits_only_usage_event_with_nothing_billable()
    {
        var subscriber = NewSubscriber();

        var events = subscriber.RecordUsage(500, Plan, October);

        var usage = Assert.IsType<UsageRecorded>(Assert.Single(events));
        Assert.Equal(500, usage.UsedMegabytes);
        Assert.Equal(0, usage.BillableMegabytes);
        Assert.Equal(202610, usage.Period);
        Assert.Equal(500, subscriber.UsedMegabytesInPeriod);
    }

    [Fact]
    public void Crossing_80_percent_emits_threshold_event()
    {
        var subscriber = NewSubscriber();
        subscriber.RecordUsage(700, Plan, October);

        var events = subscriber.RecordUsage(150, Plan, October);

        var threshold = Assert.Single(events.OfType<QuotaThresholdReached>());
        Assert.Equal(80, threshold.ThresholdPercent);
        Assert.Equal(850, threshold.UsedMegabytes);
        Assert.Equal(1_000, threshold.QuotaMegabytes);
    }

    [Fact]
    public void Each_threshold_is_announced_once_per_period()
    {
        var subscriber = NewSubscriber();
        subscriber.RecordUsage(850, Plan, October);

        var events = subscriber.RecordUsage(50, Plan, October);

        Assert.Empty(events.OfType<QuotaThresholdReached>());
    }

    [Fact]
    public void Jumping_past_several_thresholds_announces_only_the_highest()
    {
        var subscriber = NewSubscriber();

        var events = subscriber.RecordUsage(1_200, Plan, October);

        var threshold = Assert.Single(events.OfType<QuotaThresholdReached>());
        Assert.Equal(100, threshold.ThresholdPercent);
        Assert.Equal(100, subscriber.LastNotifiedThreshold);
    }

    [Fact]
    public void Only_usage_beyond_the_quota_is_billable()
    {
        var subscriber = NewSubscriber();
        subscriber.RecordUsage(900, Plan, October);

        var usage = subscriber.RecordUsage(300, Plan, October).OfType<UsageRecorded>().Single();

        Assert.Equal(200, usage.BillableMegabytes);
        Assert.Equal(0.05m, usage.OveragePricePerMegabyte);
    }

    [Fact]
    public void New_month_resets_usage_and_thresholds()
    {
        var subscriber = NewSubscriber();
        subscriber.RecordUsage(1_200, Plan, October);
        var november = October.AddMonths(1);

        var events = subscriber.RecordUsage(100, Plan, november);

        Assert.Equal(202611, subscriber.CurrentPeriod);
        Assert.Equal(100, subscriber.UsedMegabytesInPeriod);
        Assert.Equal(0, subscriber.LastNotifiedThreshold);
        Assert.Equal(0, events.OfType<UsageRecorded>().Single().BillableMegabytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_usage_is_rejected(long megabytes)
    {
        var subscriber = NewSubscriber();

        Assert.Throws<ArgumentOutOfRangeException>(() => subscriber.RecordUsage(megabytes, Plan, October));
    }

    [Fact]
    public void Usage_with_another_plan_is_rejected()
    {
        var subscriber = NewSubscriber();
        var otherPlan = new Plan { Code = "OTHER", Name = "Other", MonthlyQuotaMegabytes = 10 };

        Assert.Throws<InvalidOperationException>(() => subscriber.RecordUsage(10, otherPlan, October));
    }
}
