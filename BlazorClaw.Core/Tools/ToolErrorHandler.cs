using System.Text.Json;

namespace BlazorClaw.Core.Tools;

public static class ToolErrorHandler
{
    public static string ToProblemDetailsJson(Exception ex, string toolName, int status = 500)
    {
        var typeName = ex.GetType().Name;
        var problemDetails = new Dictionary<string, object>
        {
            { "type", $"https://tools.blazorclaw.dev/errors/{typeName}#{typeName}" },
            { "title", "Tool error" },
            { "status", status },
            { "detail", ex.Message },
            { "tool", toolName }
        };

        return JsonSerializer.Serialize(problemDetails);
    }

    public static string ToNotFoundJson(string toolName)
    {
        var problemDetails = new Dictionary<string, object>
        {
            { "type", "https://tools.blazorclaw.dev/errors/tool-not-found" },
            { "title", "Tool not found" },
            { "status", 404 },
            { "detail", $"The tool '{toolName}' was not found in the registry." },
            { "tool", toolName }
        };

        return JsonSerializer.Serialize(problemDetails);
    }
}
