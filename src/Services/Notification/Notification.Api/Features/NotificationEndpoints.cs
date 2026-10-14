using Microsoft.EntityFrameworkCore;
using Notification.Api.Data;

namespace Notification.Api.Features;

public sealed record NotificationResponse(
    Guid Id, Guid EventId, string Channel, string Recipient, string Message, int Attempts, DateTime SentAtUtc);

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/notifications/{subscriberId:guid}", async (Guid subscriberId, NotificationDbContext db, CancellationToken ct) =>
        {
            var notifications = await db.Notifications.AsNoTracking()
                .Where(n => n.SubscriberId == subscriberId)
                .OrderByDescending(n => n.SentAtUtc)
                .Take(100)
                .Select(n => new NotificationResponse(n.Id, n.EventId, n.Channel.ToString(), n.Recipient,
                    n.Message, n.Attempts, n.SentAtUtc))
                .ToListAsync(ct);
            return Results.Ok(notifications);
        }).WithTags("Notifications");
    }
}
