using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notification.Api.Consumers;
using Notification.Api.Data;
using Notification.Api.Domain;
using Notification.Api.Features;
using Notification.Api.Messaging;
using Notification.Api.Senders;
using TelecomPlatform.Messaging;

var builder = WebApplication.CreateBuilder(args);

var postgres = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

builder.Services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(postgres));

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<SenderOptions>(builder.Configuration.GetSection(SenderOptions.SectionName));
builder.Services.AddSingleton<RabbitMqConnection>();
builder.Services.AddSingleton<NotificationQueue>();

foreach (var channel in Enum.GetValues<NotificationChannel>())
{
    builder.Services.AddSingleton<INotificationSender>(sp => new LoggingSender(
        channel, sp.GetRequiredService<IOptions<SenderOptions>>(), sp.GetRequiredService<ILogger<LoggingSender>>()));
}

builder.Services.AddKafka(builder.Configuration);
builder.Services.AddHostedService<QuotaThresholdConsumer>();
builder.Services.AddHostedService<NotificationDispatcher>();

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks().AddNpgSql(postgres);

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();

app.MapNotificationEndpoints();
app.MapHealthChecks("/health");

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.MigrateAsync();
}

app.Run();
