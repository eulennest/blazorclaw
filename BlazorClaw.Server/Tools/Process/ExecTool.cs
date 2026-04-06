using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace BlazorClaw.Server.Tools.Process;

public class ExecParams : BaseToolParams
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

    [Description("Max Output in Bytes (default: 65536 = 64KB)")]
    public int? OutputLimit { get; set; } = 65536;
}

public class ExecTool : BaseTool<ExecParams>
{
    public override string Name => "process_exec";
    public override string Description => "Führt ein Programm mit Parametern aus";

    protected override async Task<string> ExecuteInternalAsync(ExecParams p, MessageContext context)
    {
        p.WorkingDirectory ??= "./";
        await p.ResolveVarsAsync(context);
        var vpath = VfsPath.Parse(PathUtils.VfsHome, p.WorkingDirectory);
        var vfs = context.Provider.GetRequiredService<IVfsSystem>();
        var path = await vfs.VfsToRealPathAsync(vpath) ?? throw new InvalidPathException(p.WorkingDirectory);
        await vfs.CreateDirectoryRecursiveAsync(vpath);
        p.Args = await SecureArgsAsync(vfs, vpath, p.Args).ConfigureAwait(false);

        var startInfo = new ProcessStartInfo
        {
            FileName = p.Binary,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = path
        };

        foreach (var arg in p.Args) startInfo.ArgumentList.Add(arg);

        using var process = System.Diagnostics.Process.Start(startInfo) ?? throw new InvalidOperationException("Prozess konnte nicht gestartet werden.");
        var cs = new CancellationTokenSource(TimeSpan.FromSeconds(p.Timeout ?? 60));
        await process.WaitForExitAsync(cs.Token).ConfigureAwait(false);
        var exited = process.HasExited;
        if (!exited) process.Kill(true);

        var outputLimit = p.OutputLimit ?? 65536;

        var sb = new System.Text.StringBuilder();
        if (!exited)
            sb.AppendLine("WARNING: Prozess hat das Zeitlimit überschritten und wurde beendet.");
        sb.AppendLine("ExitCode: " + process.ExitCode);
        sb.AppendLine("Output:");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(outputTask, errorTask);
        var output = outputTask.Result;
        var error = errorTask.Result;

        if (output.Length > outputLimit)
        {
            sb.AppendLine(output.Substring(0, outputLimit));
            sb.AppendLine($"\n[OUTPUT TRUNCATED — {output.Length - outputLimit} bytes gekürzt. Nutze OutputLimit parameter für mehr.]");
        }
        else
        {
            sb.AppendLine(output);
        }

        sb.AppendLine("Error:");
        if (error.Length > outputLimit)
        {
            sb.AppendLine(error.Substring(0, outputLimit));
            sb.AppendLine($"\n[ERROR OUTPUT TRUNCATED — {error.Length - outputLimit} bytes gekürzt.]");
        }
        else
        {
            sb.AppendLine(error);
        }

        return sb.ToString();
    }

    public async Task<string[]> SecureArgsAsync(IVfsSystem vfs, VfsPath workingdir, string[] args)
    {
        var list = new List<string>();
        foreach (var item in args)
        {
            if (item.Contains('/') || item.Contains('\\'))
            {
                var vfspath = VfsPath.Parse(workingdir, item);
                var relpath = await vfs.VfsToRealPathAsync(vfspath).ConfigureAwait(false);
                if (relpath == null)
                {
                    var basep = await vfs.VfsToRealPathAsync(workingdir).ConfigureAwait(false);
                    if (basep == null) continue;
                    var fullPath = Path.Combine(basep, item);

                    // Prüfe ob Path selbst existiert
                    if (fullPath != null)
                    {
                        if (File.Exists(fullPath) || Directory.Exists(fullPath))
                        {
                            throw new InvalidPathException($"Path '{item}' exists outside VFS");
                        }

                        // Prüfe auch das Parent-Directory (für neue Dateien)
                        var parentDir = Path.GetDirectoryName(fullPath);
                        if (parentDir != null && (Directory.Exists(parentDir) && !parentDir.StartsWith(basep)))
                        {
                            throw new InvalidPathException($"Parent directory of '{item}' is outside VFS");
                        }
                    }
                }
                else
                    list.Add(relpath);
            }
            else
                list.Add(item);
        }

        return list.ToArray();
    }

}
