namespace TelecomPlatform.Messaging;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9094";
    public string ConsumerGroupId { get; set; } = "telecom-platform";
}
