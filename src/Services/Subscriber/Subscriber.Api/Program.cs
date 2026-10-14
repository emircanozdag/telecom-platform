using Microsoft.EntityFrameworkCore;
using Subscriber.Api.Caching;
using Subscriber.Api.Data;
using Subscriber.Api.Features;
using Subscriber.Api.Outbox;
using TelecomPlatform.Messaging;

var builder = WebApplication.CreateBuilder(args);

var postgres = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

builder.Services.AddDbContext<SubscriberDbContext>(options => options.UseNpgsql(postgres));
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "subscriber:";
});
builder.Services.AddSingleton<SubscriberCache>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddKafka(builder.Configuration);
if (builder.Configuration.GetValue("Outbox:Enabled", true))
{
    builder.Services.AddHostedService<OutboxPublisher>();
}

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks().AddNpgSql(postgres);

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();

app.MapSubscriberEndpoints();
app.MapHealthChecks("/health");

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<SubscriberDbContext>().Database.MigrateAsync();
}

app.Run();

public partial class Program;
