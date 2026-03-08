using BlazorClaw.Core.Memory;

namespace BlazorClaw.Server.Memory;

public class FileSystemMemoryOptions
{
    public const string Section = "FileSystemMemory";
    public string Path { get; set; } = "./memory";
}

public class FileSystemMemorySearchProvider(Microsoft.Extensions.Options.IOptions<FileSystemMemoryOptions> options) : IMemorySearchProvider
{
    private readonly string _path = options.Value.Path;

    public async IAsyncEnumerable<string> SearchAsync(string[] queries, int maxResults)
    {
        if (Directory.Exists(_path))
        {

            var files = Directory.GetFiles(_path, "*.md", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var lines = await File.ReadAllLinesAsync(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith('#'))
                    {
                        int sectionStart = i;
                        int sectionEnd = i + 1;
                        while (sectionEnd < lines.Length && !lines[sectionEnd].StartsWith('#'))
                            sectionEnd++;

                        var section = lines.Skip(sectionStart).Take(sectionEnd - sectionStart).ToArray();
                        var sectionText = string.Join("\n", section);

                        if (queries.Any(q => sectionText.Contains(q, StringComparison.OrdinalIgnoreCase)))
                        {
                            yield return $"File: {Path.GetFileName(file)}\n{sectionText}";
                        }
                        i = sectionEnd - 1;
                    }
                }
            }
        }
    }
}
