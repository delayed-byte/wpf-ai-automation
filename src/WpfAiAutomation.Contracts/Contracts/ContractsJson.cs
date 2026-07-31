using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfAiAutomation.Contracts;

public static class ContractsJson
{
    public static JsonSerializerOptions Default { get; } = CreateDefaultOptions();

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Default);

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
