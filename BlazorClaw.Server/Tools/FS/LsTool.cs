using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using Microsoft.Extensions.FileSystemGlobbing;
using System.ComponentModel;
using System.Net;
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
        var path = VfsPath.Parse(PathUtils.VfsHome, p.Path, VfsPathParseMode.Directory);

        var entrys = p.Recursive ?? false ? vfs.GetSubPathsRecursiveAsync(path) : vfs.GetSubPathsAsync(path);
        var details = p.Details ?? false;

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(p.Pattern ?? "*");

        var sbLimited = new StringBuilder();
        var sbAll = new StringBuilder();
        var c = 0;
        var hidden = 0;
        var limit = p.Recursive ?? false ? 50 : 200;
        var sbCurrent = sbAll;
        if (details) sbCurrent.AppendLine("path\tedittime\tsize");

        await foreach (var entry in entrys.Where(o => matcher.Match(o.EntityName ?? string.Empty).HasMatches || matcher.Match(o.ToString()).HasMatches))
        {
            var f = await vfs.GetMetaInfoAsync(entry);
            c++;
            if (c > limit)
            {
                if (hidden == 0)
                {
                    sbLimited.Append(sbCurrent);
                    sbCurrent = sbAll;
                }
                hidden++;
            }
            if (f.Path.IsFile)
            {
                sbCurrent.AppendLine(details ? $"{f.Path}\t{f.LastWriteTime.ToUnix()}\t{f.Length}" : f.Path.ToString());
            }
            else
            {
                sbCurrent.AppendLine(details ? $"{f.Path}\t{f.LastWriteTime.ToUnix()}" : f.Path.ToString());
            }
        }
        if (hidden > 0)
            sbLimited.AppendLine($"[OUTPUT begrenzt auf {c - hidden}  files, {hidden} files übersprungen]");

        if (c == 0) return "Keine Dateien gefunden.";
        return (hidden > limit / 2) ? sbLimited.ToString() : sbAll.ToString();
    }
}
