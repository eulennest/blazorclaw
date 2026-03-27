using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using Microsoft.Extensions.FileSystemGlobbing;
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

    protected override async Task<string> ExecuteInternalAsync(LsParams p, MessageContext context)
    {
        var vfs = context.Provider.GetRequiredService<IVfsSystem>();
        var path = VfsPath.Parse(VfsPath.Parse("/~/"), p.Path, VfsPathParseMode.Directory);

        var entrys = p.Recursive ?? false ? vfs.GetSubPathsRecursiveAsync(path) : vfs.GetSubPathsAsync(path);
        var details = p.Details ?? false;

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(p.Pattern ?? "*");

        var sb = new StringBuilder();
        var sbhidden = new StringBuilder();
        if (details) sb.AppendLine("path\tedittime\tsize");
        var c = 0;
        var hidden = 0;
        var limit = p.Recursive ?? false ? 50 : 200;
        var sbu = sb;

        await foreach (var entry in entrys.Where(o => matcher.Match(o.EntityName).HasMatches || matcher.Match(o.ToString()).HasMatches))
        {
            var f = await vfs.GetMetaInfoAsync(entry);
            c++;
            if (c > limit)
            {
                hidden++;
                sbhidden.Append(sb);
                sbu = sbhidden;
            }
            if (f.Path.IsFile)
            {
                sbu.AppendLine(details ? $"{f.Path}\t{f.LastWriteTime.ToUnix()}\t{f.Length}" : f.Path.ToString());
            }
            else
            {
                sbu.AppendLine(details ? $"{f.Path}\t{f.LastWriteTime.ToUnix()}" : f.Path.ToString());
            }
        }
        if (hidden > 0)
            sb.AppendLine($"[OUTPUT begrenzt auf {c - hidden}  files, {hidden} files übersprungen]");

        if (c == 0) return "Keine Dateien gefunden.";
        return (hidden > limit / 2) ? sbhidden.ToString() : sb.ToString();
    }
}
