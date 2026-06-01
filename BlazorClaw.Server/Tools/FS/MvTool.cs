using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;


namespace BlazorClaw.Server.Tools.Memory;

public class MvTool : BaseTool<MvTool.Params>
{
    public override string Name => "fs_mv";
    public override string Description => "Rename eine Datei im Dateisystem.";

    public class Params
    {
        [Required, Description("Alter Dateiname")]
        public string OldFileName { get; set; } = string.Empty;

        [Required, Description("Neuer Dateiname")]
        public string NewFileName { get; set; } = string.Empty;
    }

    protected override async Task<string> ExecuteInternalAsync(Params p, MessageContext context)
    {
        var vfs = context.Provider.GetRequiredService<IVfsSystem>();
        var oldPath = VfsPath.Parse(PathUtils.VfsHome, p.OldFileName);
        if (oldPath.IsDirectory)
            throw new FileNotFoundException($"Path ist keine Datei: {p.OldFileName}");

        var newPath = VfsPath.Parse(PathUtils.VfsHome, p.NewFileName);
        if (newPath.IsDirectory) newPath.AppendFile(oldPath.EntityName!);

        if (!await vfs.ExistsAsync(oldPath))
            throw new FileNotFoundException($"Datei nicht gefunden: {p.OldFileName}");
        if(await vfs.ExistsAsync(newPath))
            throw new IOException($"Ziel existiert bereits: {p.NewFileName}");

        await vfs.MoveAsync(oldPath, newPath);
        return $"OK File {p.OldFileName} renamed to {p.NewFileName}.";
    }
}
