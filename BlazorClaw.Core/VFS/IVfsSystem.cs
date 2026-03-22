namespace BlazorClaw.Core.VFS
{
    public interface IVfsSystem : IDisposable
    {
        IAsyncEnumerable<VfsPath> GetSubPathsAsync(VfsPath path, CancellationToken cancelationToken = default);
        IAsyncEnumerable<VfsPath> GetDirectorysAsync(VfsPath path, CancellationToken cancelationToken = default);
        IAsyncEnumerable<VfsPath> GetFilesAsync(VfsPath path, CancellationToken cancelationToken = default);
        Task<VfsPathInfo> GetMetaInfoAsync(VfsPath path, CancellationToken cancelationToken = default);
        Task<bool> ExistsAsync(VfsPath path, CancellationToken cancelationToken = default);
        Task CreateFileAsync(VfsPath path, Stream data, CancellationToken cancelationToken = default);
        Task<Stream> OpenFileAsync(VfsPath path, FileMode mode, FileAccess access, CancellationToken cancelationToken = default);
        Task CreateDirectoryAsync(VfsPath path, CancellationToken cancelationToken = default);
        Task DeleteAsync(VfsPath path, CancellationToken cancelationToken = default);
    }
}