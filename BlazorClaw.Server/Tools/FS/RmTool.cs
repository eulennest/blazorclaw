using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.FS;

public class RmParams : IWorkingPaths
{
    [Description("Pfad der zu löschenden Datei/Ordner")]
    [Required]
    public string Path { get; set; } = string.Empty;

    public bool Recursive { get; set; } = false;
    public IEnumerable<string> GetPaths()
    {
        yield return Path;
    }
}

public class RmTool : BaseTool<RmParams>
{
    public override string Name => "fs_rm";
    public override string Description => "Löscht Dateien oder Verzeichnisse";

    protected override async Task<string> ExecuteInternalAsync(RmParams p, MessageContext context)
    {
        var vfs = context.Provider.GetRequiredService<IVfsSystem>();

        var path = VfsPath.Parse(VfsPath.Parse("/~/"), p.Path);

        if (!await vfs.ExistsAsync(path))
            throw new FileNotFoundException($"Der Path '{path}' wurde nicht gefunden.", p.Path);

        if (p.Recursive)
            await vfs.DeleteRecursiveAsync(path);
        else
            await vfs.DeleteAsync(path);

        return "Gelöscht.";
    }
}
