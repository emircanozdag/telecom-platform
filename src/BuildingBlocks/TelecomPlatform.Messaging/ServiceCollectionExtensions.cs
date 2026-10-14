using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TelecomPlatform.Messaging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKafka(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        return services;
    }
}
