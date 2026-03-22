namespace BlazorClaw.Core.Services
{
    public interface IMemoryService
    {
        Task<List<UserInfo>> GetActiveUsersAsync();
        Task<List<FileInfo>> GetUserMemoryFilesAsync(string userId);
        Task<string> GetUserMemoryFileContentAsync(string userId, string path);
        Task SaveUserMemoryFileAsync(string userId, string path, string content);
        Task DeleteUserMemoryFileAsync(string userId, string path);
    }

    public class UserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    public class FileInfo
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime Modified { get; set; }
        public long ModifiedUnix { get; set; }
    }
}
