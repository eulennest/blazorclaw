using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using Microsoft.Extensions.Options;

namespace BlazorClaw.Server.Security;

public class SandboxSecurityProvider(IOptionsMonitor<SandboxOptions>? options) : IToolPolicyProvider
{
    public IEnumerable<ITool> FilterTools(IEnumerable<ITool> allTools, MessageContext context) => allTools;

    public void BeforeTool(ITool tool, object parameters, MessageContext context)
    {
        if (options?.CurrentValue.AllowedPaths.Any() ?? false)
        {
            var allowed = options.CurrentValue.AllowedPaths.Select(Path.GetFullPath).ToHashSet();
            string baseFolder = options.CurrentValue.AllowedPaths.First();
            if (parameters is IWorkingPaths workingPaths)
            {
                foreach (var path in workingPaths.GetPaths())
                {
                    var full = Path.Combine(baseFolder, path);
                    if (!allowed.Any(full.StartsWith))
                    {
                        throw new UnauthorizedAccessException($"Zugriff auf Pfad '{path}' verweigert!");
                    }
                }
            }
        }
    }

    public string AfterTool(ITool tool, object parameters, string result, MessageContext context)
    {
        return result;
    }
}

public class SandboxOptions
{
    public const string Section = "Security:Sandbox";
    public IEnumerable<string> AllowedPaths { get; set; } = [];
}