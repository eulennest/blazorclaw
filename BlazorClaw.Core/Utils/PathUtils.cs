using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.VFS;
using BlazorClaw.Core.VFS.Systems;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorClaw.Core.Utils
{
    public static class PathUtils
    {
        public static readonly VfsPath VfsHome = VfsPath.Parse("/~/");
        public static readonly VfsPath VfsMemory = VfsPath.Parse("/~memory/");
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

        public static async Task<MountpointVfsSystem> BuildVFSAsync(IServiceProvider sp)
        {
            var sess = sp.GetService<SessionStateAccessor>()?.SessionState?.Session;
            var userId = sess?.UserId?.ToLowerInvariant();
            var au = sp.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser? u = null;
            var uuid = Guid.NewGuid();
            if (string.IsNullOrWhiteSpace(userId))
            {
                var aus = sp.GetRequiredService<IHttpContextAccessor>();
                if (aus.HttpContext?.User != null)
                {
                    u = await au.GetUserAsync(aus.HttpContext.User).ConfigureAwait(false);
                    if (u != null) userId = u.Id;
                    if (!Guid.TryParse(userId, out uuid)) uuid = sess?.Id ?? Guid.NewGuid();
                }
            }
            u ??= await au.FindByIdAsync(userId).ConfigureAwait(false);
            var roles = u != null ? await au.GetRolesAsync(u).ConfigureAwait(false) : ["Guest"];
            var conf = sp.GetRequiredService<IOptions<SecurityOptions>>();

            var sandboxDisabled = false;
            var sandboxPaths = new HashSet<string>();
            foreach (var item in conf.Value.UserGroups)
            {
                if (!roles.Contains(item.Key, IgnoreCaseEqualityComparer.Instance)) continue;
                if (!item.Value.Sandbox.Enabled)
                {
                    sandboxDisabled = true;
                    break;
                }
                foreach (var spath in item.Value.Sandbox.Paths)
                {
                    sandboxPaths.Add(spath);
                }
            }

            var userBaseFolder = GetUserBasePath(sp, uuid);
            var vfs = new MountpointVfsSystem();
            Directory.CreateDirectory(userBaseFolder);
            if (Directory.Exists(userBaseFolder))
            {
                Directory.CreateDirectory(Path.Combine(userBaseFolder, "workspace"));
                vfs.AddMountpoint(VfsPath.Parse("/~/"), new PhysicalFileSystem(Path.Combine(userBaseFolder, "workspace")));
                Directory.CreateDirectory(Path.Combine(userBaseFolder, "memory"));
                vfs.AddMountpoint(VfsPath.Parse("/~memory/"), new PhysicalFileSystem(Path.Combine(userBaseFolder, "memory")));
                Directory.CreateDirectory(Path.Combine(userBaseFolder, "secure"));
                vfs.AddMountpoint(VfsPath.Parse("/~secure/"), new PhysicalFileSystem(Path.Combine(userBaseFolder, "secure")), true);
            }

            if (sandboxDisabled)
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
                    vfs.AddMountpoint(VfsPath.Root, new PhysicalFileSystem("/"));
                }
            }
            else
            {
                vfs.AddMountpoint(VfsPath.Parse(VfsPath.Root, Environment.CurrentDirectory, VfsPathParseMode.Directory), new NoOpFileSystem(), true);

                foreach (var item in sandboxPaths)
                {
                    vfs.AddMountpoint(VfsPath.Parse(VfsPath.Root, item, VfsPathParseMode.Directory), new PhysicalFileSystem(item), true);
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
