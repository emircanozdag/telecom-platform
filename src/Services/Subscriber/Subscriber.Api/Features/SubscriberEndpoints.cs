using Microsoft.EntityFrameworkCore;
using Npgsql;
using Subscriber.Api.Caching;
using Subscriber.Api.Data;
using Subscriber.Api.Outbox;

namespace Subscriber.Api.Features;

public static class SubscriberEndpoints
{
    private const int MaxConcurrencyRetries = 5;

    public static void MapSubscriberEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/plans", GetPlans).WithTags("Plans");

        var subscribers = app.MapGroup("/api/subscribers").WithTags("Subscribers");
        subscribers.MapPost("/", CreateSubscriber);
        subscribers.MapGet("/{id:guid}", GetSubscriber).WithName("GetSubscriber");
        subscribers.MapPost("/{id:guid}/usage", RecordUsage);
    }

    private static async Task<IResult> GetPlans(SubscriberDbContext db, CancellationToken ct)
    {
        var plans = await db.Plans.AsNoTracking()
            .OrderBy(p => p.MonthlyQuotaMegabytes)
            .Select(p => new PlanResponse(p.Code, p.Name, p.MonthlyQuotaMegabytes, p.OveragePricePerMegabyte))
            .ToListAsync(ct);
        return Results.Ok(plans);
    }

    private static async Task<IResult> CreateSubscriber(
        CreateSubscriberRequest request, SubscriberDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var errors = RequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var plan = await db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Code == request.PlanCode, ct);
        if (plan is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.PlanCode)] = [$"Unknown plan '{request.PlanCode}'."],
            });
        }

        var subscriber = new Domain.Subscriber(
            request.Msisdn, request.Email.Trim(), request.FullName.Trim(), plan.Code, clock.GetUtcNow().UtcDateTime);
        db.Subscribers.Add(subscriber);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.Conflict(new { error = $"MSISDN {request.Msisdn} is already registered." });
        }

        return Results.CreatedAtRoute("GetSubscriber", new { id = subscriber.Id }, ToResponse(subscriber, plan.MonthlyQuotaMegabytes));
    }

    private static async Task<IResult> GetSubscriber(
        Guid id, SubscriberDbContext db, SubscriberCache cache, CancellationToken ct)
    {
        var cached = await cache.GetAsync(id, ct);
        if (cached is not null)
        {
            return Results.Ok(cached);
        }

        var response = await db.Subscribers.AsNoTracking()
            .Where(s => s.Id == id)
            .Join(db.Plans, s => s.PlanCode, p => p.Code, (s, p) => ToResponse(s, p.MonthlyQuotaMegabytes))
            .FirstOrDefaultAsync(ct);

        if (response is null)
        {
            return Results.NotFound();
        }

        await cache.SetAsync(response, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> RecordUsage(
        Guid id, RecordUsageRequest request, SubscriberDbContext db, SubscriberCache cache,
        TimeProvider clock, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var errors = RequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // Concurrent usage reports for the same subscriber race on the xmin row version; retry on conflict.
        for (var attempt = 1; ; attempt++)
        {
            var subscriber = await db.Subscribers.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (subscriber is null)
            {
                return Results.NotFound();
            }

            var plan = await db.Plans.AsNoTracking().FirstAsync(p => p.Code == subscriber.PlanCode, ct);
            var events = subscriber.RecordUsage(request.Megabytes, plan, clock.GetUtcNow().UtcDateTime);

            // State change and its events commit atomically.
            OutboxWriter.Enqueue(db, subscriber.Id, events);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (attempt >= MaxConcurrencyRetries)
                {
                    return Results.Conflict(new { error = "Usage is being updated concurrently; please retry." });
                }

                loggerFactory.CreateLogger(nameof(SubscriberEndpoints))
                    .LogInformation("Concurrency conflict recording usage for {SubscriberId}, attempt {Attempt}", id, attempt);
                db.ChangeTracker.Clear();
                continue;
            }

            await cache.InvalidateAsync(id, ct);
            return Results.Ok(ToResponse(subscriber, plan.MonthlyQuotaMegabytes));
        }
    }

    private static SubscriberResponse ToResponse(Domain.Subscriber s, long quota) =>
        new(s.Id, s.Msisdn, s.Email, s.FullName, s.PlanCode, s.CurrentPeriod, s.UsedMegabytesInPeriod, quota);
}
