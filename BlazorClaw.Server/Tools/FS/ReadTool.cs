using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools.FS;

public class ReadParams
{
    [Description("Pfad der zu lesenden Datei")]
    [Required]
    public string Path { get; set; } = string.Empty;
}

public class ReadTool : BaseTool<ReadParams>
{
    public override string Name => "fs_read";
    public override string Description => "Liest den Inhalt einer Datei";

    protected override async Task<string> ExecuteInternalAsync(ReadParams p, ToolContext context)
    {
        if (!File.Exists(p.Path)) return "Datei nicht gefunden.";
        return await File.ReadAllTextAsync(p.Path);
    }
}
