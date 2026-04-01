using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using System.Text;

namespace BlazorClaw.Server.Tools.Memory;

public class MemoryLsTool : BaseTool<MemoryLsTool.Params>
{
    public override string Name => "memory_ls";
    public override string Description => "Listet alle Memory-Dateien im memory auf.";

    public class Params { }

    protected override async Task<string> ExecuteInternalAsync(Params parameters, MessageContext context)
    {
        var badChars = (from codepoint in Enumerable.Range(0, 255)
                        let ch = (char)codepoint
                        where char.IsWhiteSpace(ch)
                              || ch == '!' || ch == '?' || ch == '#' || ch == '-'
                        select ch).ToArray();

        var vfs = context.Provider.GetRequiredService<IVfsSystem>();
        var entrys = vfs.GetSubPathsRecursiveAsync(PathUtils.VfsMemory).Where(o => o.EntityName?.EndsWith(".md") ?? false);

        var sb = new StringBuilder();
        var sbhidden = new StringBuilder();
        sb.AppendLine("path\tedittime\tsize\ttitle");
        var c = 0;
        await foreach (var entry in entrys)
        {
            c++;
            var f = await vfs.GetMetaInfoAsync(entry);
            using var strm = await f.OpenReadAsync();
            var title = string.Empty;
            var safeFileName = entry.MakeRelative(PathUtils.VfsMemory);

            using var st = new StreamReader(strm);
            title = await st.ReadLineAsync() ?? string.Empty;
            title = title.Replace(entry.EntityName!, "", StringComparison.InvariantCultureIgnoreCase).Replace("  ", " ").Trim(badChars);
            sb.AppendLine($"{safeFileName}\t{f.LastWriteTime.ToUnix()}\t{f.Length}\t{title}");
        }

        if (c == 0) return "Keine Memory-Dateien gefunden.";
        return sb.ToString();
    }
}