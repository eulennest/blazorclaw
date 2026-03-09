using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using System.ComponentModel;

namespace BlazorClaw.Server.Tools.FS;

public class LsParams : IWorkingPaths
{
    [Description("Pfad zum auflisten")]
    public string Path { get; set; } = ".";

    [Description("Glob-Muster für die Dateisuche (z.B. *.txt)")]
    public string Pattern { get; set; } = "*";

    [Description("Detaillierte Informationen wie Größe und Rechte zurückgeben")]
    public bool? Details { get; set; } = false;

    [Description("Rekursiv durch Unterverzeichnisse suchen")]
    public bool? Recursive { get; set; } = false;
    public IEnumerable<string> GetPaths()
    {
        yield return Path;
    }
}

public class LsTool : BaseTool<LsParams>
{
    public override string Name => "fs_ls";
    public override string Description => "Listet Dateien in einem Verzeichnis auf (optional mit Glob-Pattern, Details und Rekursion)";

    protected override Task<string> ExecuteInternalAsync(LsParams p, MessageContext context)
    {
        if (!Directory.Exists(p.Path)) return Task.FromResult("Pfad nicht gefunden");
        var searchOption = (p.Recursive ?? false) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var entries = Directory.GetFileSystemEntries(p.Path, p.Pattern ?? "*", searchOption);

        if (p.Details != true)
            return Task.FromResult(entries.Length > 0 ? string.Join("\n", entries.Select(
                o => Directory.Exists(o) ? $"{Path.GetFileName(o)}/" : Path.GetFileName(o)
                )) : "Keine Dateien gefunden");

        var details = entries.Select(f =>
        {
            var info = new FileInfo(f);
            var isDir = (File.GetAttributes(f) & FileAttributes.Directory) == FileAttributes.Directory;
            return $"{Path.GetFileName(f)} | {(isDir ? "DIR" : info.Length + " bytes")} | {(isDir ? "N/A" : info.Attributes.ToString())}";
        });

        return Task.FromResult(string.Join("\n", details));
    }
}
