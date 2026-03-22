using BlazorClaw.Core.Data;
using BlazorClaw.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace BlazorClaw.Server.Services
{
    public class MemoryService : IMemoryService
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _db;

        public MemoryService(IConfiguration config, IWebHostEnvironment env, ApplicationDbContext db)
        {
            _config = config;
            _env = env;
            _db = db;
        }

        public async Task<List<UserInfo>> GetActiveUsersAsync()
        {
            string basePath = _config.GetValue<string>("Folders:UserData") ?? "userdata";
            var fullBasePath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, basePath));

            if (!Directory.Exists(fullBasePath))
                return new();

            // Scan folders in basePath
            var userFolders = Directory.GetDirectories(fullBasePath)
                .Where(d => Guid.TryParse(Path.GetFileName(d), out _))
                .Select(d => Path.GetFileName(d))
                .ToList();

            if (!userFolders.Any())
                return new();

            // Match folder GUIDs to users in DB
            var guids = userFolders.Select(f => Guid.Parse(f)).ToList();
            var dbUsers = await _db.Users
                .Where(u => guids.Contains(Guid.Parse(u.Id)))
                .Select(u => new UserInfo { Id = u.Id, UserName = u.UserName, FirstName = u.FirstName, LastName = u.LastName })
                .OrderBy(u => u.UserName)
                .ToListAsync();

            return dbUsers;
        }

        public Task<List<Core.Services.FileInfo>> GetUserMemoryFilesAsync(string userId)
        {
            var basePath = GetMemoryPath(userId);
            if (!Directory.Exists(basePath))
                return Task.FromResult(new List<Core.Services.FileInfo>());

            var directory = new DirectoryInfo(basePath);
            var files = directory.EnumerateFiles("*.md", SearchOption.AllDirectories)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => new Core.Services.FileInfo
                {
                    Path = f.FullName[basePath.Length..],
                    Name = f.Name,
                    Size = f.Length,
                    Modified = f.LastWriteTimeUtc,
                    ModifiedUnix = ((long)(f.LastWriteTimeUtc - new DateTime(1970, 1, 1)).TotalSeconds)
                })
                .ToList();

            return Task.FromResult(files);
        }

        public Task<string> GetUserMemoryFileContentAsync(string userId, string path)
        {
            var filePath = ValidateAndGetPath(userId, path);
            if (filePath == null || !File.Exists(filePath))
                return Task.FromResult(string.Empty);

            var content = File.ReadAllText(filePath);
            return Task.FromResult(content);
        }

        public Task SaveUserMemoryFileAsync(string userId, string path, string content)
        {
            var filePath = ValidateAndGetPath(userId, path);
            if (filePath == null)
                throw new InvalidOperationException("Invalid path");

            var directory = Path.GetDirectoryName(filePath);
            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(filePath, content ?? string.Empty);
            return Task.CompletedTask;
        }

        public Task DeleteUserMemoryFileAsync(string userId, string path)
        {
            var filePath = ValidateAndGetPath(userId, path);
            if (filePath == null || !File.Exists(filePath))
                throw new FileNotFoundException("File not found");

            File.Delete(filePath);
            return Task.CompletedTask;
        }

        private string? ValidateAndGetPath(string userId, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            path = path.Trim('/', '\\');
            var basePath = GetMemoryPath(userId);

            // Ensure path ends with .md
            if (!path.EndsWith(".md"))
                path += ".md";

            var fullPath = Path.GetFullPath(Path.Combine(basePath, path));

            // Security: ensure path is within basePath
            if (!fullPath.StartsWith(basePath))
                return null;

            return fullPath;
        }

        private string GetMemoryPath(string userId)
        {
            string basePath = _config.GetValue<string>("Folders:UserData") ?? "userdata";
            var fullBasePath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, basePath)).TrimEnd(Path.DirectorySeparatorChar);
            return Path.Combine(fullBasePath, userId, "memory") + Path.DirectorySeparatorChar;
        }
    }

}
