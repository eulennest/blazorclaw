namespace BlazorClaw.Core.VFS
{
    public interface IVfsSystem : IDisposable
    {
        IAsyncEnumerable<VfsPath> GetSubPathsAsync(VfsPath path, CancellationToken cancellationToken = default);
        IAsyncEnumerable<VfsPath> GetDirectorysAsync(VfsPath path, CancellationToken cancellationToken = default);
        IAsyncEnumerable<VfsPath> GetFilesAsync(VfsPath path, CancellationToken cancellationToken = default);
        ValueTask<VfsPathInfo> GetMetaInfoAsync(VfsPath path, CancellationToken cancellationToken = default);
        ValueTask<bool> ExistsAsync(VfsPath path, CancellationToken cancellationToken = default);
        Task CreateFileAsync(VfsPath path, Stream data, CancellationToken cancellationToken = default);
        Task<Stream> OpenFileAsync(VfsPath path, FileMode mode, FileAccess access, CancellationToken cancellationToken = default);
        Task CreateDirectoryAsync(VfsPath path, CancellationToken cancellationToken = default);
        Task DeleteAsync(VfsPath path, CancellationToken cancellationToken = default);
        Task DeleteRecursiveAsync(VfsPath path, CancellationToken cancellationToken = default);
        ValueTask MoveAsync(VfsPath pathFrom, VfsPath pathTo, CancellationToken cancellationToken = default);

        ValueTask<string?> VfsToRealPathAsync(VfsPath path, CancellationToken cancellationToken = default);
        ValueTask<VfsPath?> RealToVfsPathAsync(string path, CancellationToken cancellationToken = default);
    }
}