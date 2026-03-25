using BlazorClaw.Core.Commands;
using BlazorClaw.Core.VFS;
using BlazorClaw.Core.VFS.Systems;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorClaw.Core.Utils
{
    public static class PathUtils
    {
        public static string GetAllBasePath(MessageContext context)
        {
            return GetAllBasePath(context.Provider);
        }

        public static string GetAllBasePath(IServiceProvider provider)
        {
            var conf = provider.GetRequiredService<IConfiguration>();
            var ienv = provider.GetRequiredService<IWebHostEnvironment>();
            string basePath = conf.GetValue<string>("Folders:UserData") ?? "userdata";
            return Path.GetFullPath(Path.Combine(ienv.ContentRootPath, basePath)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }
        public static string GetUserBasePath(MessageContext context)
        {
            var userId = context.UserId?.ToLowerInvariant();
            if (!Guid.TryParse(userId, out var uuid)) uuid = context.Session?.Id ?? Guid.NewGuid();
            return GetUserBasePath(context.Provider, uuid);
        }

        public static string GetUserBasePath(IServiceProvider provider, Guid userId)
        {
            var basePath = GetAllBasePath(provider);
            return Path.GetFullPath(Path.Combine(basePath, userId.ToString()));
        }

        public static string GetWorkspacePath(this MessageContext context)
        {
            var basePath = GetUserBasePath(context);
            return Path.GetFullPath(Path.Combine(basePath, "workspace")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        public static string GetMemoryBasePath(this MessageContext context)
        {
            var basePath = GetUserBasePath(context);
            return Path.GetFullPath(Path.Combine(basePath, "memory")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
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

        public static IVfsSystem BuildVFS(string userBaseFolder, bool addRoot)
        {
            var vfs = new MountpointVfsSystem();
            if (Directory.Exists(userBaseFolder))
            {
                Directory.CreateDirectory(Path.Combine(userBaseFolder, "workspace"));
                vfs.AddMountpoint(VfsPath.Parse("/~/"), new PhysicalFileSystem(Path.Combine(userBaseFolder, "workspace")));
                Directory.CreateDirectory(Path.Combine(userBaseFolder, "memory"));
                vfs.AddMountpoint(VfsPath.Parse("/~memory/"), new PhysicalFileSystem(Path.Combine(userBaseFolder, "memory")));
                Directory.CreateDirectory(Path.Combine(userBaseFolder, "secure"));
                vfs.AddMountpoint(VfsPath.Parse("/~secure/"), new PhysicalFileSystem(Path.Combine(userBaseFolder, "secure")), true);
            }

            if (addRoot)
            {
                if (OperatingSystem.IsWindows())
                {
                    foreach (var item in DriveInfo.GetDrives())
                    {
                        vfs.AddMountpoint(VfsPath.Parse($"/{item.Name.Trim(':', '/')}/"), new PhysicalFileSystem(item.RootDirectory.FullName));
                    }
                }
                else
                {
                    vfs.AddMountpoint(VfsPath.Root, new PhysicalFileSystem("/"), true);
                }
            }
            return vfs;

        }
    }

    public static class Extensions
    {
        public static long ToUnix(this DateTime time)
        {
            return (long)time.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        public static Task<T?> NoThrow<T>(this Task<T> t)
        {
            return t?.ContinueWith(delegate (Task<T> o)
            {
                return o.IsFaulted || o.IsCanceled ? default : o.Result;
            }) ?? Task.FromResult<T?>(default);
        }

        public static Task NoThrow(this Task t)
        {
            return t?.ContinueWith(delegate (Task o) { }) ?? Task.CompletedTask;
        }
    }
}
