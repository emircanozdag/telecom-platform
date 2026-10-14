using Billing.Api.Data;
using Billing.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TelecomPlatform.Contracts;

namespace Billing.Api.Consumers;

public sealed class UsageRecordedHandler(BillingDbContext db, ILogger<UsageRecordedHandler> logger)
{
    public async Task HandleAsync(UsageRecorded message, CancellationToken ct)
    {
        if (await db.ProcessedEvents.AnyAsync(e => e.EventId == message.EventId, ct))
        {
            logger.LogDebug("Skipping duplicate event {EventId}", message.EventId);
            return;
        }

        var invoice = await db.Invoices.FindAsync([message.SubscriberId, message.Period], ct);
        if (invoice is null)
        {
            invoice = new Invoice(message.SubscriberId, message.Msisdn, message.Period);
            db.Invoices.Add(invoice);
        }

        invoice.AddUsage(message.UsedMegabytes, message.BillableMegabytes,
            message.OveragePricePerMegabyte, message.OccurredAtUtc);
        db.ProcessedEvents.Add(new ProcessedEvent { EventId = message.EventId, ProcessedAtUtc = DateTime.UtcNow });

        try
        {
            // Invoice update and inbox record commit together.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another consumer instance processed the same event first. The constraint violation rolled back
            // our whole transaction, so nothing was double-counted. Throwing makes the consumer retry, and
            // the retry sees the event as processed.
            throw new InvalidOperationException($"Event {message.EventId} was processed concurrently; retrying.", ex);
        }
    }
}
