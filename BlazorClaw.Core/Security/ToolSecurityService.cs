using BlazorClaw.Core.Tools;

namespace BlazorClaw.Core.Security;

public interface IToolSecurityInjector
{
    void BeforeTool(ITool tool, object parameters, ToolContext context);
    void AfterTool(ITool tool, object parameters, string result, ToolContext context);
}

public class ToolSecurityService : IToolSecurityInjector
{
    public void BeforeTool(ITool tool, object parameters, ToolContext context)
    {
        // Beispiel: Nur Admin darf "fs_rm" ausführen
        if (tool.Name == "fs_rm" && context.UserId != "admin")
        {
            throw new UnauthorizedAccessException($"Tool {tool.Name} erfordert Admin-Rechte.");
        }
    }

    public void AfterTool(ITool tool, object parameters, string result, ToolContext context)
    {
        // Hier könnte später Audit-Logging rein
    }
}
