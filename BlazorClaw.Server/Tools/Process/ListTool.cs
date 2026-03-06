using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools.Process;

public class ListParams { }

public class ListTool : BaseTool<ListParams>
{
    public override string Name => "process_list";
    public override string Description => "Listet alle laufenden Prozesse auf";

    protected override Task<string> ExecuteInternalAsync(ListParams p, ToolContext context)
    {
        var processes = System.Diagnostics.Process.GetProcesses();
        return Task.FromResult(string.Join("\n", processes.Select(pr => $"{pr.Id}: {pr.ProcessName}")));
    }
}
