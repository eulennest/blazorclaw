using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Services;

namespace BlazorClaw.Server.Tools.Memory
{
    public class MemoryToolUtils
    {
        public static string GetMemoryBasePath(MessageContext context)
        {
            var pathH = context.Provider.GetRequiredService<PathHelper>();
            var userId = context.UserId?.ToLowerInvariant();
            if (!Guid.TryParse(userId, out var uuid)) uuid = context.Session?.Id ?? Guid.NewGuid();
            return Path.GetFullPath(Path.Combine(pathH.GetBaseFolder(), "memory", uuid.ToString()));
        }

        public static string GetMemoryPath(string filename, MessageContext context)
        {
            var path = GetMemoryBasePath(context);
            if (!filename.EndsWith(".md")) filename += ".md";
            var fpath = Path.GetFullPath(Path.Combine(path, filename));
            if (!fpath.StartsWith(path)) throw new InvalidOperationException("Path not allowed");
            return fpath;
        }
    }
}
    