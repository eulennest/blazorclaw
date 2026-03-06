using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools.Memory;

public class MemoryLsTool : BaseTool<MemoryLsTool.Params>
{
    private readonly string _memoryPath = "./memory";

    public override string Name => "memory_ls";
    public override string Description => "Listet alle Memory-Dateien im /memory Ordner auf.";

    public class Params { }

    protected override Task<string> ExecuteInternalAsync(Params parameters, ToolContext context)
    {
        if (!Directory.Exists(_memoryPath))
            return Task.FromResult("Memory-Ordner existiert nicht.");

        var files = Directory.GetFiles(_memoryPath, "*.md")
                             .Select(Path.GetFileName)
                             .ToList();

        if (files.Count == 0)
            return Task.FromResult("Keine Memory-Dateien gefunden.");

        return Task.FromResult("Memory-Dateien:\n- " + string.Join("\n- ", files));
    }
}
