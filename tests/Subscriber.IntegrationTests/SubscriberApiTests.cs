using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Subscriber.Api.Data;
using Subscriber.Api.Features;
using TelecomPlatform.Contracts;

namespace Subscriber.IntegrationTests;

public class SubscriberApiTests(SubscriberApiFactory factory) : IClassFixture<SubscriberApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string RandomMsisdn() => $"905{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

    private async Task<SubscriberResponse> CreateSubscriberAsync(string plan = "MINI-5GB")
    {
        var response = await _client.PostAsJsonAsync("/api/subscribers",
            new CreateSubscriberRequest(RandomMsisdn(), "user@example.com", "Test User", plan));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SubscriberResponse>())!;
    }

    [Fact]
    public async Task Lists_seeded_plans()
    {
        var plans = await _client.GetFromJsonAsync<List<PlanResponse>>("/api/plans");

        Assert.NotNull(plans);
        Assert.Contains(plans, p => p.Code == "MINI-5GB" && p.MonthlyQuotaMegabytes == 5_120);
    }

    [Fact]
    public async Task Creates_and_reads_a_subscriber()
    {
        var created = await CreateSubscriberAsync();

        var fetched = await _client.GetFromJsonAsync<SubscriberResponse>($"/api/subscribers/{created.Id}");

        Assert.Equal(created, fetched);
    }

    [Fact]
    public async Task Rejects_duplicate_msisdn()
    {
        var msisdn = RandomMsisdn();
        var request = new CreateSubscriberRequest(msisdn, "a@example.com", "A", "MINI-5GB");
        await _client.PostAsJsonAsync("/api/subscribers", request);

        var duplicate = await _client.PostAsJsonAsync("/api/subscribers", request);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Theory]
    [InlineData("12345", "user@example.com", "MINI-5GB")]
    [InlineData("905321234567", "not-an-email", "MINI-5GB")]
    [InlineData("905321234567", "user@example.com", "NO-SUCH-PLAN")]
    public async Task Rejects_invalid_requests(string msisdn, string email, string plan)
    {
        var response = await _client.PostAsJsonAsync("/api/subscribers",
            new CreateSubscriberRequest(msisdn, email, "Test User", plan));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Recording_usage_writes_events_to_the_outbox_atomically()
    {
        var subscriber = await CreateSubscriberAsync(); // 5,120 MB quota

        var response = await _client.PostAsJsonAsync($"/api/subscribers/{subscriber.Id}/usage", new RecordUsageRequest(4_200));
        response.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SubscriberDbContext>();
        var eventTypes = await db.OutboxMessages
            .Where(m => m.Key == subscriber.Id.ToString())
            .Select(m => m.EventType)
            .ToListAsync();

        Assert.Equal(2, eventTypes.Count);
        Assert.Contains(nameof(UsageRecorded), eventTypes);
        Assert.Contains(nameof(QuotaThresholdReached), eventTypes);
    }

    [Fact]
    public async Task Usage_invalidates_the_cached_subscriber()
    {
        var subscriber = await CreateSubscriberAsync();
        await _client.GetFromJsonAsync<SubscriberResponse>($"/api/subscribers/{subscriber.Id}"); // warm cache

        await _client.PostAsJsonAsync($"/api/subscribers/{subscriber.Id}/usage", new RecordUsageRequest(100));
        var fetched = await _client.GetFromJsonAsync<SubscriberResponse>($"/api/subscribers/{subscriber.Id}");

        Assert.Equal(100, fetched!.UsedMegabytesInPeriod);
    }

    [Fact]
    public async Task Concurrent_usage_reports_are_not_lost()
    {
        var subscriber = await CreateSubscriberAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            _client.PostAsJsonAsync($"/api/subscribers/{subscriber.Id}/usage", new RecordUsageRequest(10))));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var fetched = await _client.GetFromJsonAsync<SubscriberResponse>($"/api/subscribers/{subscriber.Id}");
        Assert.Equal(50, fetched!.UsedMegabytesInPeriod);
    }

    [Fact]
    public async Task Unknown_subscriber_returns_404()
    {
        var response = await _client.GetAsync($"/api/subscribers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
