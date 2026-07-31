using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfAiAutomation.Contracts;

public static class ContractsJson
{
    public static JsonSerializerOptions Default { get; } = CreateDefaultOptions();

    private static JsonSerializerOptions Compact { get; } = new(Default) { WriteIndented = false };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Default);

    public static string SerializeCompact<T>(T value) => JsonSerializer.Serialize(value, Compact);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Default);

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = true,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
