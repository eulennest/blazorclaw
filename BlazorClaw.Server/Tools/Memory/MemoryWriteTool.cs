using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using ReverseMarkdown.Converters;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Memory;

public class MemoryWriteTool : BaseTool<MemoryWriteTool.Params>
{
    public override string Name => "memory_write";
    public override string Description => "Schreibt Markdown-Inhalt in eine Memory-Datei (relative zum /memory Ordner).";

    public enum WriteMode { Create, Append, Override }

    public class Params
    {
        [Required, Description("Dateiname (immer relativ, Name + .md)")]
        public string FileName { get; set; } = string.Empty;

        [Required, Description("Markdown-Inhalt der geschrieben werden soll")]
        public string Content { get; set; } = string.Empty;

        [Description("Modus: Create (Standard, gibt Fehler wenn schon existiert), Append oder Override")]
        public WriteMode? Mode { get; set; } = WriteMode.Create;
    }

    protected override async Task<string> ExecuteInternalAsync(Params p, MessageContext context)
    {
        var vfs = context.Provider.GetRequiredService<IVfsSystem>();
        if (p.FileName.StartsWith('/')) p.FileName = p.FileName[1..];
        var path = VfsPath.Parse(PathUtils.VfsMemory, p.FileName);
        if (path.IsDirectory)
            throw new FileNotFoundException($"Path ist keine Datei: {p.FileName}");
        if (!PathUtils.VfsMemory.IsParentOf(path)) throw new InvalidPathException(p.FileName);

        var safeFileName = path.MakeRelative(PathUtils.VfsMemory);

        var mi = await vfs.GetMetaInfoAsync(path);
        var mode = p.Mode ?? WriteMode.Create;
        if (mode == WriteMode.Append)
        {
            using var stream = await mi.OpenAsync(FileMode.Append, FileAccess.Write);
            using var reader = new StreamWriter(stream);
            await reader.WriteAsync(p.Content);
            return $"Inhalt erfolgreich an {safeFileName} angehängt.";
        }
        else
        {
            if (mode == WriteMode.Create && mi.Exists)
                throw new InvalidOperationException($"Datei existiert bereits: {safeFileName}");

            await vfs.WriteAllTextAsync(path, p.Content);

            return $"Memory-Datei erfolgreich gespeichert unter: {safeFileName}";
        }
    }
}
