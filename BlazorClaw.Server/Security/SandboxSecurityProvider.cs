using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Security;

namespace BlazorClaw.Server.Security;

public class SandboxSecurityProvider : IMessagePolicyProvider, IToolPolicyProvider
{
    private readonly string _basePath;

    public SandboxSecurityProvider(string basePath = "./")
    {
        _basePath = Path.GetFullPath(basePath);
    }

    // --- IToolPolicyProvider (Tools filtern) ---
    public IEnumerable<ITool> FilterTools(IEnumerable<ITool> allTools, ToolContext context)
    {
        return allTools; // Alle Tools erlaubt
    }

    public void BeforeTool(ITool tool, string arguments, ToolContext context)
    {
        // Parameter-Validierung: Versuchen zu entkommen?
        if (arguments.Contains(".."))
        {
             throw new UnauthorizedAccessException("Sandbox violation: Path traversal detected.");
        }
    }

    public string AfterTool(ITool tool, string arguments, string result, ToolContext context)
    {
        return result;
    }

    // --- IMessagePolicyProvider (Nachrichten filtern) ---
    public string FilterUserMessage(string message, ToolContext context) => message;
    public string FilterAssistantMessage(string message, ToolContext context) => message;
}
