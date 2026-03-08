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

        var directory = new DirectoryInfo(_memoryPath);
        var files = directory.GetFiles("*.md");

        if (files.Length == 0)
            return Task.FromResult("Keine Memory-Dateien gefunden.");

        var fileInfoList = files.Select(f =>
            $"- {f.Name} (Größe: {f.Length} Bytes, Letzte Änderung: {f.LastWriteTime:yyyy-MM-dd HH:mm:ss}, Attribute: {f.Attributes})");

        return Task.FromResult("Memory-Dateien:\n" + string.Join("\n", fileInfoList));
    }
}
