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
            return Path.Combine(pathH.GetBaseFolder(), "memory", uuid.ToString());
        }

        public static string GetMemoryPath(string filename, MessageContext context)
        {
            var path = GetMemoryBasePath(context);
            var safeFileName = Path.GetFileName(filename);
            if (!safeFileName.EndsWith(".md")) safeFileName += ".md";
            return Path.Combine(path, safeFileName);
        }
    }
}
