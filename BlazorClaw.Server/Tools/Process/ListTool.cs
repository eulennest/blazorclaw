using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using System.ComponentModel;

namespace BlazorClaw.Server.Tools.Process;

public class ListParams
{
    [Description("Optionaler Filterbegriff für den Prozessnamen")]
    public string? Search { get; set; }
}

public class ListTool : BaseTool<ListParams>
{
    public override string Name => "process_list";
    public override string Description => "Listet laufende Prozesse auf (optional gefiltert)";

    protected override Task<string> ExecuteInternalAsync(ListParams p, MessageContext context)
    {
        var processes = System.Diagnostics.Process.GetProcesses();
        if (!string.IsNullOrEmpty(p.Search))
        {
            processes = [.. processes.Where(pr => pr.ProcessName.Contains(p.Search, StringComparison.OrdinalIgnoreCase))];
        }

        return Task.FromResult(processes.Length > 0
            ? string.Join("\n", processes.Select(pr => $"{pr.Id}: {pr.ProcessName}"))
            : "Keine übereinstimmenden Prozesse gefunden.");
    }
}
