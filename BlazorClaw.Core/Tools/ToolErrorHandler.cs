using System.Text.Json;

namespace BlazorClaw.Core.Tools;

public class ToolNotFoundException(string toolName) : Exception($"Tool '{toolName}' not found.")
{
    public string ToolName { get; } = toolName;
}

public static class ToolErrorHandler
{
    public static string ToProblemDetailsJson(Exception ex, string toolName, int? status = null)
    {
        var typeName = ex.GetType().Name.Replace("Exception", "");
        status ??= typeName.Contains("NotFound") ? 404 : 500;

        var problemDetails = new Dictionary<string, object>
    {
        { "type", typeName },
        { "status", status },
        { "detail", ex.Message },
   //         { "stacktrace", ex.StackTrace ?? string.Empty },
        { "tool", toolName }
    };

        if (ex.InnerException != null)
            problemDetails["inner"] = ex.InnerException.Message;

        return JsonSerializer.Serialize(problemDetails);
    }
}
