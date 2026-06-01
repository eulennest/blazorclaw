using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;


namespace BlazorClaw.Server.Tools.Memory;

public class MemoryMvTool : BaseTool<MemoryMvTool.Params>
{
    public override string Name => "memory_mv";
    public override string Description => "Rename eine Memory-Datei (relative zum /memory Ordner).";

    public class Params
    {
        [Required, Description("Alter Dateiname (immer relativ, Name + .md)")]
        public string OldFileName { get; set; } = string.Empty;

        [Required, Description("Neuer Dateiname (immer relativ, Name + .md)")]
        public string NewFileName { get; set; } = string.Empty;
    }

    protected override async Task<string> ExecuteInternalAsync(Params p, MessageContext context)
    {
        var vfs = context.Provider.GetRequiredService<IVfsSystem>();
        if (p.OldFileName.StartsWith('/')) p.OldFileName = p.OldFileName[1..];
        var oldPath = VfsPath.Parse(PathUtils.VfsMemory, p.OldFileName);
        if (oldPath.IsDirectory)
            throw new FileNotFoundException($"Path ist keine Datei: {p.OldFileName}");
        if (!PathUtils.VfsMemory.IsParentOf(oldPath)) throw new InvalidPathException(p.OldFileName);

        if(p.NewFileName.StartsWith('/')) p.NewFileName = p.NewFileName[1..];
        var newPath = VfsPath.Parse(PathUtils.VfsMemory, p.NewFileName);
        if (newPath.IsDirectory) newPath.AppendFile(oldPath.EntityName!);
        if (!PathUtils.VfsMemory.IsParentOf(newPath)) throw new InvalidPathException(p.NewFileName);

        if (!await vfs.ExistsAsync(oldPath))
            throw new FileNotFoundException($"Memory-Datei nicht gefunden: {p.OldFileName}");
        if(await vfs.ExistsAsync(newPath))
            throw new IOException($"Ziel existiert bereits: {p.NewFileName}");

        await vfs.MoveAsync(oldPath, newPath);
        return $"OK Memory File {p.OldFileName} renamed to {p.NewFileName}.";
    }
}
