using Billing.Api.Consumers;
using Billing.Api.Data;
using Billing.Api.Features;
using Microsoft.EntityFrameworkCore;
using TelecomPlatform.Messaging;

var builder = WebApplication.CreateBuilder(args);

var postgres = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

builder.Services.AddDbContext<BillingDbContext>(options => options.UseNpgsql(postgres));
builder.Services.AddKafka(builder.Configuration);
builder.Services.AddScoped<UsageRecordedHandler>();
builder.Services.AddHostedService<UsageRecordedConsumer>();

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks().AddNpgSql(postgres);

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();

app.MapBillingEndpoints();
app.MapHealthChecks("/health");

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<BillingDbContext>().Database.MigrateAsync();
}

app.Run();
