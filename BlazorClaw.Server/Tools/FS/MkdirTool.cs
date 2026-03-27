using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.FS;

public class MkdirParams : IWorkingPaths
{
    [Description("Pfad des neuen Verzeichnisses")]
    [Required]
    public string Path { get; set; } = string.Empty;
    public IEnumerable<string> GetPaths()
    {
        yield return Path;
    }
}

public class MkdirTool : BaseTool<MkdirParams>
{
    public override string Name => "fs_mkdir";
    public override string Description => "Erstellt ein neues Verzeichnis";

    protected override async Task<string> ExecuteInternalAsync(MkdirParams p, MessageContext context)
    {
        var vfs = context.Provider.GetRequiredService<IVfsSystem>();
        var path = VfsPath.Parse(VfsPath.Parse("/~/"), p.Path, VfsPathParseMode.Directory);
        await vfs.CreateDirectoryRecursiveAsync(path);
        return $"Verzeichnis {p.Path} erstellt.";
    }
}
