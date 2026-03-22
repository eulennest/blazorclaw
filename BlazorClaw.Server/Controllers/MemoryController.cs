using BlazorClaw.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace BlazorClaw.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class MemoryController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _db;

        public MemoryController(IConfiguration config, IWebHostEnvironment env, ApplicationDbContext db)
        {
            _config = config;
            _env = env;
            _db = db;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _db.Users
                .Select(u => new { u.Id, u.UserName, u.FirstName, u.LastName })
                .OrderBy(u => u.UserName)
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("user/{userId}/files")]
        public IActionResult GetUserMemoryFiles(string userId)
        {
            var basePath = GetMemoryPath(userId);
            if (!Directory.Exists(basePath))
                return Ok(new { files = Array.Empty<object>() });

            var directory = new DirectoryInfo(basePath);
            var files = directory.EnumerateFiles("*.md", SearchOption.AllDirectories)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => new
                {
                    path = f.FullName[basePath.Length..],
                    name = f.Name,
                    size = f.Length,
                    modified = f.LastWriteTimeUtc,
                    modifiedUnix = ((long)(f.LastWriteTimeUtc - new DateTime(1970, 1, 1)).TotalSeconds)
                })
                .ToList();

            return Ok(new { files });
        }

        [HttpGet("user/{userId}/file")]
        public IActionResult GetUserMemoryFile(string userId, [FromQuery] string path)
        {
            var filePath = ValidateAndGetPath(userId, path);
            if (filePath == null)
                return BadRequest("Invalid path");

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var content = System.IO.File.ReadAllText(filePath);
            return Ok(new { content });
        }

        [HttpPost("user/{userId}/file")]
        public IActionResult SaveUserMemoryFile(string userId, [FromQuery] string path, [FromBody] SaveFileRequest request)
        {
            var filePath = ValidateAndGetPath(userId, path);
            if (filePath == null)
                return BadRequest("Invalid path");

            var directory = Path.GetDirectoryName(filePath);
            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            System.IO.File.WriteAllText(filePath, request.Content ?? string.Empty);
            return Ok(new { message = "File saved" });
        }

        [HttpDelete("user/{userId}/file")]
        public IActionResult DeleteUserMemoryFile(string userId, [FromQuery] string path)
        {
            var filePath = ValidateAndGetPath(userId, path);
            if (filePath == null)
                return BadRequest("Invalid path");

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            System.IO.File.Delete(filePath);
            return Ok(new { message = "File deleted" });
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

    public class SaveFileRequest
    {
        public string? Content { get; set; }
    }
}
