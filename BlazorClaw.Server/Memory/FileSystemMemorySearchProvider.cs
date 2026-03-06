using BlazorClaw.Core.Memory;

namespace BlazorClaw.Server.Memory;

public class FileSystemMemoryOptions
{
    public const string Section = "FileSystemMemory";
    public string Path { get; set; } = "./memory";
}

public class FileSystemMemorySearchProvider : IMemorySearchProvider
{
    private readonly string _path;

    public FileSystemMemorySearchProvider(Microsoft.Extensions.Options.IOptions<FileSystemMemoryOptions> options)
    {
        _path = options.Value.Path;
    }

    public async Task<string> SearchAsync(string[] queries, int maxResults)
    {
        var results = new List<string>();
        if (!Directory.Exists(_path)) return "Memory Verzeichnis nicht gefunden.";
        
        var files = Directory.GetFiles(_path, "*.md", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var lines = await File.ReadAllLinesAsync(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("#"))
                {
                    int sectionStart = i;
                    int sectionEnd = i + 1;
                    while (sectionEnd < lines.Length && !lines[sectionEnd].StartsWith("#"))
                        sectionEnd++;
                    
                    var section = lines.Skip(sectionStart).Take(sectionEnd - sectionStart).ToArray();
                    var sectionText = string.Join("\n", section);

                    if (queries.Any(q => sectionText.Contains(q, StringComparison.OrdinalIgnoreCase)))
                    {
                        results.Add($"File: {Path.GetFileName(file)}\n{sectionText}");
                    }
                    i = sectionEnd - 1;
                }
            }
            if (results.Count >= maxResults) break;
        }

        return results.Any() ? string.Join("\n\n---\n\n", results) : "Keine Treffer im Memory.";
    }
}
