using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools.FS;

public class GrepParams
{
    [Description("Pfad zum Suchen")]
    public string Path { get; set; } = ".";
    
    [Description("Suchtext in Dateien")]
    [Required]
    public string Query { get; set; } = string.Empty;
}

public class GrepTool : BaseTool<GrepParams>
{
    public override string Name => "fs_grep";
    public override string Description => "Sucht nach Textinhalt in Dateien innerhalb eines Pfades";

    protected override async Task<string> ExecuteInternalAsync(GrepParams p, ToolContext context)
    {
        var results = new List<string>();
        foreach (var file in Directory.GetFiles(p.Path, "*", SearchOption.AllDirectories))
        {
            var content = await File.ReadAllTextAsync(file);
            if (content.Contains(p.Query))
            {
                results.Add(file);
            }
        }
        return results.Count > 0 ? string.Join("\n", results) : "Keine Treffer.";
    }
}
