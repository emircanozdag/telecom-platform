using Billing.Api.Domain;

namespace Billing.UnitTests;

public class InvoiceTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Accumulates_usage_and_overage()
    {
        var invoice = new Invoice(Guid.NewGuid(), "905321234567", 202610);

        invoice.AddUsage(500, 0, 0.05m, Now);
        invoice.AddUsage(300, 200, 0.05m, Now.AddMinutes(5));

        Assert.Equal(800, invoice.TotalMegabytes);
        Assert.Equal(200, invoice.OverageMegabytes);
        Assert.Equal(10.00m, invoice.OverageAmount);
        Assert.Equal(2, invoice.UsageRecordCount);
        Assert.Equal(Now.AddMinutes(5), invoice.LastUsageAtUtc);
    }

    [Fact]
    public void Each_charge_is_rounded_to_two_decimals()
    {
        var invoice = new Invoice(Guid.NewGuid(), "905321234567", 202610);

        invoice.AddUsage(3, 3, 0.0333m, Now); // 0.0999 -> 0.10
        invoice.AddUsage(3, 3, 0.0333m, Now); // 0.0999 -> 0.10

        Assert.Equal(0.20m, invoice.OverageAmount);
    }

    [Fact]
    public void Out_of_order_events_keep_the_latest_usage_time()
    {
        var invoice = new Invoice(Guid.NewGuid(), "905321234567", 202610);

        invoice.AddUsage(10, 0, 0m, Now);
        invoice.AddUsage(10, 0, 0m, Now.AddHours(-1));

        Assert.Equal(Now, invoice.LastUsageAtUtc);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, -1)]
    [InlineData(10, 11)]
    public void Rejects_inconsistent_usage(long used, long billable)
    {
        var invoice = new Invoice(Guid.NewGuid(), "905321234567", 202610);

        Assert.Throws<ArgumentException>(() => invoice.AddUsage(used, billable, 0.05m, Now));
    }
}
