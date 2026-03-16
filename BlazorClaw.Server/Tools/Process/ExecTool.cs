using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace BlazorClaw.Server.Tools.Process;

public class ExecParams
{
    [Description("Pfad zur ausführbaren Datei")]
    [Required]
    public string Binary { get; set; } = string.Empty;

    [Description("Argumente für die Datei  (zb. ['-t' ,'test name', '-f'])")]
    public string[] Args { get; set; } = [];

    [Description("Timeout in Sekunden (default: 60)")]
    public int? Timeout { get; set; } = 60;

    [Description("Arbeitsverzeichnis für den Befehl (optional default: ./)")]
    public string? WorkingDirectory { get; set; } = "./";

}

public class ExecTool : BaseTool<ExecParams>
{
    public override string Name => "process_exec";
    public override string Description => "Führt ein Programm mit Parametern aus";

    protected override async Task<string> ExecuteInternalAsync(ExecParams p, MessageContext context)
    {
        var path = Path.Combine(context.GetWorkspacePath(), p.WorkingDirectory ?? "./repos");

        var startInfo = new ProcessStartInfo
        {
            FileName = p.Binary,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = path
        };

        foreach (var arg in p.Args) startInfo.ArgumentList.Add(arg);

        using var process = System.Diagnostics.Process.Start(startInfo) ?? throw new InvalidOperationException("Prozess konnte nicht gestartet werden.");
        var cs = new CancellationTokenSource(TimeSpan.FromSeconds(p.Timeout ?? 60));
        await process.WaitForExitAsync(cs.Token).ConfigureAwait(false);
        var exited = process.HasExited;
        if (!exited) process.Kill(true);
        var sb = new System.Text.StringBuilder();
        if (!exited)
            sb.AppendLine("WARNING: Prozess hat das Zeitlimit überschritten und wurde beendet.");
        sb.AppendLine("ExitCode: " + process.ExitCode);
        sb.AppendLine("Output:");
        sb.AppendLine(process.StandardOutput.ReadToEnd());
        sb.AppendLine("Error:");
        sb.AppendLine(process.StandardError.ReadToEnd());

        return sb.ToString();
    }
}
