using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Utils;

namespace BlazorClaw.Server.Tools.Memory;

public class MemoryReadTool : BaseTool<MemoryReadTool.Params>
{
    public override string Name => "memory_read";
    public override string Description => "Liest den Inhalt einer Memory-Datei als Markdown-Text (relative zum /memory Ordner).";

    public class Params
    {
        [Required, Description("Dateiname (immer relativ, Name + .md)")]
        public string FileName { get; set; } = string.Empty;
    }

    protected override async Task<string> ExecuteInternalAsync(Params parameters, MessageContext context)
    {
        var fullPath = context.GetMemoryPath(parameters.FileName);
        var safeFileName = Path.GetFileName(fullPath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Memory-Datei nicht gefunden: {safeFileName}");

        return await File.ReadAllTextAsync(fullPath);
    }
}
