using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
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

    protected override Task<string> ExecuteInternalAsync(RmParams p, MessageContext context)
    {
        var path = Path.Combine(context.GetWorkspacePath(), p.Path);

        if (File.Exists(path)) File.Delete(path);
        else if (Directory.Exists(path)) Directory.Delete(path, p.Recursive);
        else return Task.FromResult("Pfad existiert nicht.");

        return Task.FromResult("Gelöscht.");
    }
}
