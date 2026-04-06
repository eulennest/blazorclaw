using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using Microsoft.Extensions.FileSystemGlobbing;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;


namespace BlazorClaw.Server.Tools.FS;

public class GrepParams : IWorkingPaths
{
    [Description("Pfad zum Suchen")]
    public string Path { get; set; } = ".";

    [Description("Suchtext in Dateien")]
    [Required]
    public string Query { get; set; } = string.Empty;

    [Description("Glob-Muster (z.B. *.cs)  (Default: *)")]
    public string? Pattern { get; set; } = "*";

    [Description("Maximale Dateigröße in Bytes  (Default: 10240)")]
    public long? MaxFileSize { get; set; } = 1024 * 10; // 10kB

    [Description("Zeilen vor der Trefferzeile (Default: 5)")]
    public int? BeforeLines { get; set; } = 5;

    [Description("Zeilen nach der Trefferzeile (Default: 5)")]
    public int? AfterLines { get; set; } = 5;

    [Description("Rekursiv durch Unterverzeichnisse suchen  (Default: true)")]
    public bool? Recursive { get; set; } = true;
    public IEnumerable<string> GetPaths()
    {
        yield return Path;
    }
}

public class GrepTool : BaseTool<GrepParams>
{
    public override string Name => "fs_grep";
    public override string Description => "Sucht nach Textinhalt in Dateien";

    protected override async Task<string> ExecuteInternalAsync(GrepParams p, MessageContext context)
    {
        var vfs = context.Provider.GetRequiredService<IVfsSystem>();
        var path = VfsPath.Parse(PathUtils.VfsHome, p.Path, VfsPathParseMode.Directory);

        var entrys = p.Recursive ?? false ? vfs.GetSubPathsRecursiveAsync(path) : vfs.GetSubPathsRecursiveAsync(path);

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude(p.Pattern ?? "*");

        var results = new List<object>();
        var pattern = p.Pattern ?? "*";
        var maxFileSize = p.MaxFileSize ?? 1024 * 10;
        var before = p.BeforeLines ?? 5;
        var after = p.AfterLines ?? 5;


        var sb = new StringBuilder();

        await foreach (var entry in entrys.Where(o => o.IsFile && matcher.Match(o.ToString()).HasMatches))
        {
            var f = await vfs.GetMetaInfoAsync(entry);
            if (f.Length > maxFileSize) continue;

            using var stream = await f.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var lines = (await reader.ReadToEndAsync()).Split(['\r', '\n']);

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(p.Query))
                {
                    int start = Math.Max(0, i - before);
                    int end = Math.Min(lines.Length - 1, i + after);

                    results.Add(new
                    {
                        File = f.Path.ToString(),
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
