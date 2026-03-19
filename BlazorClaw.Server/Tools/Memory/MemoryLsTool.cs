using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools.Memory;

using BlazorClaw.Core.Utils;
using System.Text;

public class MemoryLsTool : BaseTool<MemoryLsTool.Params>
{
    public override string Name => "memory_ls";
    public override string Description => "Listet alle Memory-Dateien im memory auf.";

    public class Params { }

    protected override async Task<string> ExecuteInternalAsync(Params parameters, MessageContext context)
    {
        var _memoryPath = context.GetMemoryBasePath();
        var ml = _memoryPath.Length;
        if (!Directory.Exists(_memoryPath))
            return "ERROR: Memory-Ordner existiert nicht.";

        var directory = new DirectoryInfo(_memoryPath);
        var files = directory.EnumerateFiles("*.md", SearchOption.AllDirectories);
        var c = 0;

        var sb = new StringBuilder();
        sb.AppendLine("edittime\tsize\tpath\ttitle");
        foreach (var f in files)
        {
            c++;
            var title = await File.ReadLinesAsync(f.FullName).Where(o => !string.IsNullOrWhiteSpace(o)).FirstOrDefaultAsync();

            sb.AppendLine($"{f.LastWriteTime.ToUniversalTime():u}\t{f.Length}\t{f.FullName[ml..]}\t{title}");
        }

        if (c == 0) return "Keine Memory-Dateien gefunden.";
        return sb.ToString();
    }
}
