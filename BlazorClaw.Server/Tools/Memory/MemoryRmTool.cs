using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;


namespace BlazorClaw.Server.Tools.Memory;

public class MemoryRmTool : BaseTool<MemoryRmTool.Params>
{
    public override string Name => "memory_rm";
    public override string Description => "Löscht eine Memory-Datei (relative zum /memory Ordner).";

    public class Params
    {
        [Required, Description("Dateiname (immer relativ, Name + .md)")]
        public string FileName { get; set; } = string.Empty;
    }

    protected override async Task<string> ExecuteInternalAsync(Params p, MessageContext context)
    {
        var fullPath = context.GetMemoryPath(p.FileName);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Memory-Datei nicht gefunden: {p.FileName}");

        File.Delete(fullPath);
        return $"OK Memory File {p.FileName} deleted.";
    }
}
