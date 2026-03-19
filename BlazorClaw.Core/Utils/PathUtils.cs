using BlazorClaw.Core.Commands;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorClaw.Core.Utils
{
    public static class PathUtils
    {
        public static string GetAllBasePath(MessageContext context)
        {
            var conf = context.Provider.GetRequiredService<IConfiguration>();
            var ienv = context.Provider.GetRequiredService<IWebHostEnvironment>();
            string basePath = conf.GetValue<string>("Folders:UserData") ?? "userdata";
            return Path.GetFullPath(Path.Combine(ienv.ContentRootPath, basePath)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        public static string GetWorkspacePath(this MessageContext context)
        {
            var basePath = GetAllBasePath(context);
            var userId = context.UserId?.ToLowerInvariant();
            if (!Guid.TryParse(userId, out var uuid)) uuid = context.Session?.Id ?? Guid.NewGuid();
            return Path.GetFullPath(Path.Combine(basePath, uuid.ToString(), "workspace")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        public static string GetMemoryBasePath(this MessageContext context)
        {
            var basePath = GetAllBasePath(context);
            var userId = context.UserId?.ToLowerInvariant();
            if (!Guid.TryParse(userId, out var uuid)) uuid = context.Session?.Id ?? Guid.NewGuid();
            return Path.GetFullPath(Path.Combine(basePath, uuid.ToString(), "memory")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        public static string GetMemoryPath(this MessageContext context, string filename)
        {
            filename = filename.Trim('/', '\\');
            var path = GetMemoryBasePath(context);
            if (!filename.EndsWith(".md")) filename += ".md";
            var fpath = Path.GetFullPath(Path.Combine(path, filename));
            if (!fpath.StartsWith(path)) throw new InvalidOperationException("Path not allowed");
            return fpath;
        }
    }
}
