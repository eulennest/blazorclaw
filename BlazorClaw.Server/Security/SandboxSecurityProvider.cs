using BlazorClaw.Core.Security;
using BlazorClaw.Core.Security.Policies;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Commands;

namespace BlazorClaw.Server.Security;

public class SandboxSecurityProvider : IToolPolicyProvider
{
    public IEnumerable<ITool> FilterTools(IEnumerable<ITool> allTools, MessageContext context) => allTools;

    public void BeforeTool(ITool tool, object parameters, MessageContext context)
    {
        if (parameters is IWorkingPaths workingPaths)
        {
            var allowed = workingPaths.GetAllowedPaths();
            foreach (var path in allowed)
            {
                if (!path.StartsWith("/home/kkastl/.openclaw/workspace/memory/"))
                {
                    throw new UnauthorizedAccessException($"Zugriff auf Pfad {path} verweigert!");
                }
            }
        }
    }

    public string AfterTool(ITool tool, object parameters, string result, MessageContext context)
    {
        return result;
    }
}
