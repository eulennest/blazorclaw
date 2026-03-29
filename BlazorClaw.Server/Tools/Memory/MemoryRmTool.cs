using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;


namespace BlazorClaw.Server.Tools.Memory;

public class MemoryRmTool : BaseTool<MemoryRmTool.Params>
{
    public override string Name => "memory_rm";
    public override string Description => "Löscht eine Memory-Datei (relative zum /memory Ordner).";

    public class Params
    {
        [Required, Description("Dateiname (immer relativ, Name + .md)")]
        public string FileName { get; set; } = string.Empty;
    }

    protected override async Task<string> ExecuteInternalAsync(Params p, MessageContext context)
    {
        var vfs = context.Provider.GetRequiredService<IVfsSystem>();
        if (p.FileName.StartsWith('/')) p.FileName = p.FileName[1..];
        var path = VfsPath.Parse(PathUtils.VfsMemory, p.FileName);
        if (path.IsDirectory)
            throw new FileNotFoundException($"Path ist keine Datei: {p.FileName}");
        if (!PathUtils.VfsMemory.IsParentOf(path)) throw new InvalidPathException(p.FileName);

        if (!await vfs.ExistsAsync(path))
            throw new FileNotFoundException($"Memory-Datei nicht gefunden: {p.FileName}");
        await vfs.DeleteAsync(path);
        return $"OK Memory File {p.FileName} deleted.";
    }
}
