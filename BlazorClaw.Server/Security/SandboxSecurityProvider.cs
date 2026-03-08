using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Server.Tools.FS;

namespace BlazorClaw.Server.Security;

public class SandboxSecurityProvider : IToolPolicyProvider
{
    private readonly string _basePath;

    public SandboxSecurityProvider(string basePath = "./")
    {
        _basePath = Path.GetFullPath(basePath);
    }

    public IEnumerable<ITool> FilterTools(IEnumerable<ITool> allTools, ToolContext context) => allTools;

    public void BeforeTool(ITool tool, object parameters, ToolContext context)
    {
        if (parameters is RmParams rmParams)
        {
            if (rmParams.Path.Contains(".."))
                throw new UnauthorizedAccessException("Sandbox violation: Path traversal detected.");
        }
    }

    public string AfterTool(ITool tool, object parameters, string result, ToolContext context) => result;
}
