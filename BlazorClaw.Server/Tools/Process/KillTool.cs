using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Process;

public class KillParams
{
    [Description("PID des zu beendenden Prozesses")]
    [Required]
    public int Pid { get; set; }
}

public class KillTool : BaseTool<KillParams>
{
    public override string Name => "process_kill";
    public override string Description => "Beendet einen laufenden Prozess";

    protected override Task<string> ExecuteInternalAsync(KillParams p, ToolContext context)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(p.Pid);
            process.Kill();
            return Task.FromResult($"Prozess {p.Pid} beendet.");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Fehler: {ex.Message}");
        }
    }
}
