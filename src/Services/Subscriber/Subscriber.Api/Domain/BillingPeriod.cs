namespace Subscriber.Api.Domain;

public static class BillingPeriod
{
    /// <summary>Calendar-month billing period, e.g. 202610 for October 2026.</summary>
    public static int For(DateTime utc) => utc.Year * 100 + utc.Month;
}
