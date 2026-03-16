using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Process;

public class GitOptions
{
    public const string Section = "Tools:Git";
    public string GitPath { get; set; } = "git"; // Standardpfad
}

public class GitTool(IOptions<GitOptions> options) : BaseTool<GitTool.Params>
{
    private readonly GitOptions _options = options.Value;

    public override string Name => "git_exec";
    public override string Description => "Führt einen Git-Befehl aus.";

    public class Params : IWorkingPaths
    {
        [Required, Description("Git-Befehl und Argumente (z.B. 'status' oder 'log -n 5')")]
        public string Args { get; set; } = string.Empty;

        [Description("Arbeitsverzeichnis für den Git-Befehl (optional default: ./repos)")]
        public string? WorkingDirectory { get; set; } = "./repos";
        public IEnumerable<string> GetPaths()
        {
            yield return WorkingDirectory ?? "./repos";
        }
    }

    protected override async Task<string> ExecuteInternalAsync(Params p, MessageContext context)
    {
        var path = Path.Combine(context.GetWorkspacePath(), p.WorkingDirectory ?? "./repos");

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = _options.GitPath,
            Arguments = p.Args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = path
        };

        using var process = System.Diagnostics.Process.Start(startInfo) ?? throw new NullReferenceException("Prozess konnte nicht gestartet werden.");
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            return $"Fehler (Code {process.ExitCode}): {error}";

        return string.IsNullOrEmpty(output) ? "Git-Befehl erfolgreich ausgeführt." : output;
    }
}
