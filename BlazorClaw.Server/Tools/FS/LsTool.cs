using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using System.ComponentModel;
using System.Text;

namespace BlazorClaw.Server.Tools.FS;

public class LsParams : IWorkingPaths
{
    [Description("Pfad zum auflisten")]
    public string Path { get; set; } = ".";

    [Description("Glob-Muster für die Dateisuche (z.B. *.txt)")]
    public string Pattern { get; set; } = "*";

    [Description("Detaillierte Informationen wie Größe und Zeit zurückgeben")]
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
        var path = Path.Combine(context.GetWorkspacePath(), p.Path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Directory.Exists(path)) return Task.FromResult("Pfad nicht gefunden");
        var searchOption = (p.Recursive ?? false) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var ml = path.Length;
        var mdi = new DirectoryInfo(path);
        var entries = mdi.EnumerateFileSystemInfos(p.Pattern ?? "*", searchOption);
        var details = p.Details ?? false;

        var sb = new StringBuilder();
        if (details) sb.AppendLine("path\tedittime\tsize");
        var c = 0;
        foreach (var f in entries)
        {
            c++;
            if (f is FileInfo fi)
            {
                sb.AppendLine(details ? $"{f.FullName[ml..]}\t{f.LastWriteTimeUtc.ToUnix()}\t{fi.Length}" : f.FullName[ml..]);
            }
            else if (f is DirectoryInfo di)
            {
                sb.AppendLine(details ? $"{f.FullName[ml..]}{Path.DirectorySeparatorChar}\t{f.LastWriteTimeUtc.ToUnix()}" : f.FullName[ml..] + Path.DirectorySeparatorChar);
            }
        }

        if (c == 0) return Task.FromResult("Keine Dateien gefunden.");
        return Task.FromResult(sb.ToString());
    }
}
