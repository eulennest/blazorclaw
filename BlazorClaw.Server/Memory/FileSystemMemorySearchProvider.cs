using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Memory;
using BlazorClaw.Core.Utils;

namespace BlazorClaw.Server.Memory;

public class FileSystemMemorySearchProvider() : IMemorySearchProvider
{

    public async IAsyncEnumerable<string> SearchAsync(string[] queries, int maxResults, MessageContext? context)
    {
        if (context == null) yield break;
        var _path = context.GetMemoryBasePath();
        if (Directory.Exists(_path))
        {
            var pl = _path.Length;
            var files = Directory.GetFiles(_path, "*.md", SearchOption.AllDirectories);
            foreach (var file in files.OrderByDescending(File.GetLastWriteTimeUtc))
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
                            yield return $"[memory: {file[pl..]} | Line {sectionStart}-{sectionEnd} | {File.GetLastWriteTimeUtc(file):yyyyMMdd}]\n{sectionText}";
                        }
                        i = sectionEnd - 1;
                    }
                }
            }
        }
    }
}
