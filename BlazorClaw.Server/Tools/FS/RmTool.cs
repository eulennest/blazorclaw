using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
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
        if (File.Exists(p.Path)) File.Delete(p.Path);
        else if (Directory.Exists(p.Path)) Directory.Delete(p.Path, p.Recursive);
        else return Task.FromResult("Pfad existiert nicht.");

        return Task.FromResult("Gelöscht.");
    }
}
