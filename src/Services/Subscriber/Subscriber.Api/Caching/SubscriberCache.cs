using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Subscriber.Api.Features;
using TelecomPlatform.Messaging;

namespace Subscriber.Api.Caching;

/// <summary>Cache-aside for subscriber reads, backed by Redis.</summary>
public sealed class SubscriberCache(IDistributedCache cache, ILogger<SubscriberCache> logger)
{
    private static readonly DistributedCacheEntryOptions EntryOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
    };

    private static string KeyFor(Guid id) => $"subscriber:{id}";

    public async Task<SubscriberResponse?> GetAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var json = await cache.GetStringAsync(KeyFor(id), ct);
            return json is null ? null : JsonSerializer.Deserialize<SubscriberResponse>(json, JsonDefaults.Options);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The cache is an optimization: if Redis is down, fall back to the database.
            logger.LogWarning(ex, "Cache read failed for subscriber {SubscriberId}", id);
            return null;
        }
    }

    public async Task SetAsync(SubscriberResponse subscriber, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(subscriber, JsonDefaults.Options);
            await cache.SetStringAsync(KeyFor(subscriber.Id), json, EntryOptions, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache write failed for subscriber {SubscriberId}", subscriber.Id);
        }
    }

    public async Task InvalidateAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await cache.RemoveAsync(KeyFor(id), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache invalidation failed for subscriber {SubscriberId}", id);
        }
    }
}
