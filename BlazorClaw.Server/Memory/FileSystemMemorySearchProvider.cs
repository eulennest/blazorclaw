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

    public async Task<string> SearchAsync(string query, int maxResults)
    {
        var results = new List<string>();
        var files = Directory.GetFiles(_path, "*.md", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file);
            if (content.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.Add($"File: {Path.GetFileName(file)}\nContent: {content.Substring(0, Math.Min(content.Length, 300))}...");
            }
            if (results.Count >= maxResults) break;
        }

        return results.Any() ? string.Join("\n\n", results) : "Keine Treffer im Memory.";
    }
}
