using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools.FS;

public class GrepParams
{
    [Description("Pfad zum Suchen")]
    public string Path { get; set; } = ".";
    
    [Description("Suchtext in Dateien")]
    [Required]
    public string Query { get; set; } = string.Empty;

    [Description("Glob-Muster (z.B. *.cs)")]
    public string Pattern { get; set; } = "*";

    [Description("Maximale Dateigröße in Bytes")]
    public long MaxFileSize { get; set; } = 1024 * 1024; // 1MB

    [Description("Zeilen vor dem Treffer")]
    public int BeforeLines { get; set; } = 5;

    [Description("Zeilen nach dem Treffer")]
    public int AfterLines { get; set; } = 5;
}

public class GrepTool : BaseTool<GrepParams>
{
    public override string Name => "fs_grep";
    public override string Description => "Sucht nach Textinhalt in Dateien";

    protected override async Task<string> ExecuteInternalAsync(GrepParams p, ToolContext context)
    {
        var results = new List<object>();
        foreach (var file in Directory.GetFiles(p.Path, p.Pattern, SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            if (info.Length > p.MaxFileSize) continue;

            var lines = await File.ReadAllLinesAsync(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(p.Query))
                {
                    int start = Math.Max(0, i - p.BeforeLines);
                    int end = Math.Min(lines.Length - 1, i + p.AfterLines);
                    
                    results.Add(new {
                        File = file,
                        Line = i + 1,
                        Match = lines[i],
                        Context = lines.Skip(start).Take(end - start + 1).ToArray()
                    });
                }
            }
        }
        return JsonSerializer.Serialize(new { Matches = results });
    }
}
