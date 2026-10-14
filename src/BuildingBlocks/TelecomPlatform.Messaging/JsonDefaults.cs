using System.Text.Json;

namespace TelecomPlatform.Messaging;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
