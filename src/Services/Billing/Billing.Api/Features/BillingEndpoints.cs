using Billing.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Billing.Api.Features;

public sealed record InvoiceResponse(
    Guid SubscriberId,
    string Msisdn,
    int Period,
    long TotalMegabytes,
    long OverageMegabytes,
    decimal OverageAmount,
    int UsageRecordCount,
    DateTime? LastUsageAtUtc);

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var billing = app.MapGroup("/api/billing").WithTags("Billing");
        billing.MapGet("/{subscriberId:guid}/invoices", GetInvoices);
        billing.MapGet("/{subscriberId:guid}/invoices/{period:int}", GetInvoice);
    }

    private static async Task<IResult> GetInvoices(Guid subscriberId, BillingDbContext db, CancellationToken ct)
    {
        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => i.SubscriberId == subscriberId)
            .OrderByDescending(i => i.Period)
            .Select(i => new InvoiceResponse(i.SubscriberId, i.Msisdn, i.Period, i.TotalMegabytes,
                i.OverageMegabytes, i.OverageAmount, i.UsageRecordCount, i.LastUsageAtUtc))
            .ToListAsync(ct);
        return Results.Ok(invoices);
    }

    private static async Task<IResult> GetInvoice(Guid subscriberId, int period, BillingDbContext db, CancellationToken ct)
    {
        var invoice = await db.Invoices.AsNoTracking()
            .Where(i => i.SubscriberId == subscriberId && i.Period == period)
            .Select(i => new InvoiceResponse(i.SubscriberId, i.Msisdn, i.Period, i.TotalMegabytes,
                i.OverageMegabytes, i.OverageAmount, i.UsageRecordCount, i.LastUsageAtUtc))
            .FirstOrDefaultAsync(ct);
        return invoice is null ? Results.NotFound() : Results.Ok(invoice);
    }
}
