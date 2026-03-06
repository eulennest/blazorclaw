using BlazorClaw.Core.Memory;
using BlazorClaw.Server.Tools.FS;

namespace BlazorClaw.Server.Memory;

public class FileSystemMemorySearchProvider : IMemorySearchProvider
{
    private readonly string _path;

    public FileSystemMemorySearchProvider(string path)
    {
        _path = path;
    }

    public async Task<string> SearchAsync(string[] queries, int maxResults)
    {
        var results = new List<string>();
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
