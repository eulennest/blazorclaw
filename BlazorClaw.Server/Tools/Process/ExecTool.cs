using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace BlazorClaw.Server.Tools.Process;

public class ExecParams
{
    [Description("Pfad zur ausführbaren Datei")]
    [Required]
    public string Binary { get; set; } = string.Empty;

    [Description("Argumente für die Datei  (zb. ['-t' ,'test', '-f'])")]
    public string[] Args { get; set; } = [];
}

public class ExecTool : BaseTool<ExecParams>
{
    public override string Name => "process_exec";
    public override string Description => "Führt ein Programm mit Parametern aus";

    protected override Task<string> ExecuteInternalAsync(ExecParams p, MessageContext context)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = p.Binary,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var arg in p.Args) startInfo.ArgumentList.Add(arg);

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null) return Task.FromResult("Fehler beim Starten des Prozesses.");

        process.WaitForExit();
        return Task.FromResult(process.StandardOutput.ReadToEnd());
    }
}
