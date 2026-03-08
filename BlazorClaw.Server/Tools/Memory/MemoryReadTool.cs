using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Memory;

public class MemoryReadTool : BaseTool<MemoryReadTool.Params>
{
    private readonly string _memoryPath = "./memory";

    public override string Name => "memory_read";
    public override string Description => "Liest den Inhalt einer Memory-Datei als Markdown-Text (relative zum /memory Ordner).";

    public class Params
    {
        [Required, Description("Dateiname (ohne Pfad, nur Name + .md)")]
        public string FileName { get; set; } = string.Empty;
    }

    protected override async Task<string> ExecuteInternalAsync(Params parameters, ToolContext context)
    {
        var safeFileName = Path.GetFileName(parameters.FileName);
        if (!safeFileName.EndsWith(".md")) safeFileName += ".md";
        var fullPath = Path.Combine(_memoryPath, safeFileName);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Memory-Datei nicht gefunden: {safeFileName}");

        return await File.ReadAllTextAsync(fullPath);
    }
}
