using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Memory;


public class MemoryReadTool : BaseTool<MemoryReadTool.Params>
{
    public class Params
    {
        [Required, Description("Dateiname (immer relativ, Name + .md)")]
        public string FileName { get; set; } = string.Empty;

        [Description("Maximale Anzahl der zurückzugebenden Zeichen (default: unbegrenzt)")]
        public int? Limit { get; set; }

        [Description("Anzahl der Zeichen, die übersprungen werden sollen (default: 0)")]
        public int? Offset { get; set; }

    }

    public override string Name => "memory_read";
    public override string Description => "Liest den Inhalt einer Memory-Datei als Markdown-Text (relative zum /memory Ordner).";

    protected override async Task<string> ExecuteInternalAsync(Params p, MessageContext context)
    {
        if (p.Limit.HasValue && p.Limit.Value < 0) throw new ArgumentException("Limit muss größer oder gleich 0 sein.", nameof(p.Limit));
        if (p.Offset.HasValue && p.Offset.Value < 0) throw new ArgumentException("Offset muss größer oder gleich 0 sein.", nameof(p.Offset));

        if (p.FileName.StartsWith('/')) p.FileName = p.FileName[1..];
        var path = VfsPath.Parse(PathUtils.VfsMemory, p.FileName);
        if (path.IsDirectory)
            throw new FileNotFoundException($"Path ist keine Datei: {p.FileName}");
        if (!PathUtils.VfsMemory.IsParentOf(path)) throw new InvalidPathException(p.FileName);

        var vfs = context.Provider.GetRequiredService<IVfsSystem>();
        var mi = await vfs.GetMetaInfoAsync(path);
        if (!mi.Exists)
        {
            var list = await vfs.GetSubPathsRecursiveAsync(PathUtils.VfsMemory).FirstOrDefaultAsync(o => o.EntityName.EndsWith(mi.Name, StringComparison.InvariantCultureIgnoreCase));
            var mean = list.IsFile ? $" Meintest du vielleicht '{list}'?" : "";
            throw new FileNotFoundException($"Memory Datei '{p.FileName}' wurde nicht gefunden.{mean}", p.FileName);
        }

        using var stream = await mi.OpenReadAsync();
        using var reader = new StreamReader(stream);
        if (p.Offset.HasValue)
        {
            await reader.ReadAsync(new char[p.Offset.Value], 0, p.Offset.Value);
        }
        if (p.Limit.HasValue)
        {
            var sb = new System.Text.StringBuilder();
            var buffer = new char[p.Limit.Value];
            int read = await reader.ReadAsync(buffer, 0, p.Limit.Value);
            if (read > 0)
            {
                sb.Append(buffer, 0, read);
            }
            if (!reader.EndOfStream)
            {
                sb.AppendLine($"[More data in file. Use offset={p.Limit + 1} to continue.]");
            }
            return sb.ToString();
        }
        return await reader.ReadToEndAsync();
    }
}
