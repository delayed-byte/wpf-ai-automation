using System.Reflection;
using AgentServer;
using ModelContextProtocol.Server;
using WpfAiAutomation.Contracts;

namespace WpfAiAutomation.UnitTests;

public sealed class McpToolContractTests
{
    private static readonly string[] ExpectedTools =
    [
        "assert_state",
        "capture_screenshot",
        "close_application",
        "execute_plan",
        "find_element",
        "inspect_ui",
        "invoke",
        "launch_application",
        "read_element",
        "select_item",
        "set_text",
        "wait_for",
    ];

    [Fact]
    public void DiscoverySurfaceContainsExactlyTheAllowlistedTools()
    {
        var names = ToolMethods()
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()!.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedTools, names);
    }

    [Fact]
    public void ToolInputsExposeNoPathProcessCommandOrCoordinateCapabilities()
    {
        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "command", "executablePath", "filePath", "path", "processId", "x", "y", "coordinates",
        };

        var inputNames = ToolMethods()
            .SelectMany(method => method.GetParameters())
            .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
            .SelectMany(parameter => PropertyNames(parameter.ParameterType, new HashSet<Type>()))
            .ToArray();

        Assert.DoesNotContain(inputNames, forbidden.Contains);
    }

    [Fact]
    public void ToolRequestJsonUsesCamelCaseAndRejectsUnknownMembers()
    {
        var request = new SetTextRequest("session", new ElementLocator("PatientIdTextBox"), "PM-1001");
        var json = ContractsJson.Serialize(request);

        Assert.Contains("\"sessionId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"automationId\"", json, StringComparison.Ordinal);
        Assert.Throws<System.Text.Json.JsonException>(() => ContractsJson.Deserialize<SetTextRequest>(
            """{"sessionId":"session","target":{"automationId":"PatientIdTextBox"},"value":"PM-1001","command":"unsafe"}"""));
    }

    private static MethodInfo[] ToolMethods() => typeof(AutomationTools)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
        .ToArray();

    private static IEnumerable<string> PropertyNames(Type type, HashSet<Type> visited)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (!visited.Add(type) || type == typeof(string) || type.IsPrimitive || type.IsEnum)
        {
            yield break;
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var name in PropertyNames(argument, visited))
                {
                    yield return name;
                }
            }
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            yield return property.Name;
            foreach (var name in PropertyNames(property.PropertyType, visited))
            {
                yield return name;
            }
        }
    }
}
