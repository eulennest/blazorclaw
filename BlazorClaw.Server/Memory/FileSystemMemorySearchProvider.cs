using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Memory;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;

namespace BlazorClaw.Server.Memory;

public class FileSystemMemorySearchProvider() : IMemorySearchProvider
{

    public async IAsyncEnumerable<string> SearchAsync(string[] queries, int maxResults, MessageContext? context)
    {
        if (context == null) yield break;

        var vfs = context.Provider.GetRequiredService<IVfsSystem>();
        var entrys = await vfs.GetSubPathsRecursiveAsync(PathUtils.VfsMemory).Where(o => o.EntityName?.EndsWith(".md") ?? false)
            .Select(vfs.GetMetaInfoAsync).ToListAsync();

        var pl = PathUtils.VfsMemory.Path.Length;
        foreach (var file in entrys.OrderByDescending(o => o.LastWriteTime))
        {
            var lines = await vfs.ReadAllLinesAsync(file.Path);
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
                        yield return $"[memory: {file.Path.Path[pl..]} | Line {sectionStart}-{sectionEnd} | {file.LastWriteTime:yyyyMMdd}]\n{sectionText}";
                    }
                    i = sectionEnd - 1;
                }
            }
        }
    }
}