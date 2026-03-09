using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Server.Tools.FS;

namespace BlazorClaw.Server.Security;

public class SandboxSecurityProvider(string basePath = "./") : IToolPolicyProvider
{
    private readonly string _basePath = Path.GetFullPath(basePath);

    public IEnumerable<ITool> FilterTools(IEnumerable<ITool> allTools, MessageContext context) => allTools;

    public void BeforeTool(ITool tool, object parameters, MessageContext context)
    {
        if (parameters is RmParams rmParams)
        {
            if (rmParams.Path.Contains(".."))
                throw new UnauthorizedAccessException("Sandbox violation: Path traversal detected.");
        }
    }

    public string AfterTool(ITool tool, object parameters, string result, MessageContext context) => result;
}
