using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.FS;

public class ReadParams : IWorkingPaths
{
    [Description("Pfad der zu lesenden Datei")]
    [Required]
    public string Path { get; set; } = string.Empty;

    [Description("Maximale Anzahl der zurückzugebenden Zeichen (default: unbegrenzt)")]
    public int? Limit { get; set; }

    [Description("Anzahl der Zeichen, die übersprungen werden sollen (default: 0)")]
    public int? Offset { get; set; }
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
        var path = Path.Combine(context.GetWorkspacePath(), p.Path);

        if (!File.Exists(path)) throw new FileNotFoundException($"Die Datei '{p.Path}' wurde nicht gefunden.", p.Path);
        if (p.Limit.HasValue && p.Limit.Value < 0) throw new ArgumentException("Limit muss größer oder gleich 0 sein.", nameof(p.Limit));
        if (p.Offset.HasValue && p.Offset.Value < 0) throw new ArgumentException("Offset muss größer oder gleich 0 sein.", nameof(p.Offset));

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);
        if (p.Offset.HasValue)
        {
            await reader.ReadAsync(new char[p.Offset.Value], 0, p.Offset.Value);
        }
        if (p.Limit.HasValue)
        {
            var sb = new System.Text.StringBuilder();
            var buffer = new char[p.Limit.Value];
            int read = await reader.ReadAsync(buffer, 0, p.Limit.Value);
            if (read > 0)
            {
                sb.Append(buffer, 0, read);
            }
            if (!reader.EndOfStream)
            {
                sb.AppendLine($"[More data in file. Use offset={p.Limit + 1} to continue.]");
            }
            return sb.ToString();
        }
        return await reader.ReadToEndAsync();
    }
}
