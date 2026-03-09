using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.FS;

public class ReadParams : IWorkingPaths
{
    [Description("Pfad der zu lesenden Datei")]
    [Required]
    public string Path { get; set; } = string.Empty;
    public IEnumerable<string> GetPaths()
    {
        yield return Path;
    }
}

public class ReadTool : BaseTool<ReadParams>
{
    public override string Name => "fs_read";
    public override string Description => "Liest den Inhalt einer Datei";

    protected override async Task<string> ExecuteInternalAsync(ReadParams p, MessageContext context)
    {
        if (!File.Exists(p.Path)) return "Datei nicht gefunden.";
        return await File.ReadAllTextAsync(p.Path);
    }
}
