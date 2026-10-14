namespace Subscriber.Api.Features;

public sealed record CreateSubscriberRequest(string Msisdn, string Email, string FullName, string PlanCode);

public sealed record RecordUsageRequest(long Megabytes);

public sealed record SubscriberResponse(
    Guid Id,
    string Msisdn,
    string Email,
    string FullName,
    string PlanCode,
    int CurrentPeriod,
    long UsedMegabytesInPeriod,
    long MonthlyQuotaMegabytes);

public sealed record PlanResponse(string Code, string Name, long MonthlyQuotaMegabytes, decimal OveragePricePerMegabyte);
