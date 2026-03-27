namespace BlazorClaw.Core.VFS.Systems
{
    public class NoOpFileSystem : IVfsSystem
    {

        void IDisposable.Dispose()
        {
        }

        public IAsyncEnumerable<VfsPath> GetSubPathsAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return AsyncEnumerable.Empty<VfsPath>();
        }

        public IAsyncEnumerable<VfsPath> GetDirectorysAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return AsyncEnumerable.Empty<VfsPath>();
        }

        public IAsyncEnumerable<VfsPath> GetFilesAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return AsyncEnumerable.Empty<VfsPath>();
        }

        public async Task<VfsPathInfo> GetMetaInfoAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return new VfsPathInfo(VfsEntity.Create(this, path));
        }

        public Task<bool> ExistsAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return Task.FromResult(false);
        }

        public async Task CreateFileAsync(VfsPath path, Stream data, CancellationToken cancelationToken = default)
        {
            throw new InvalidOperationException();
        }

        public Task<Stream> OpenFileAsync(VfsPath path, FileMode mode, FileAccess access, CancellationToken cancelationToken = default)
        {
            throw new InvalidOperationException();
        }

        public Task CreateDirectoryAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            throw new InvalidOperationException();
        }

        public Task DeleteAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            throw new InvalidOperationException();
        }

        public Task DeleteRecursiveAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            throw new InvalidOperationException();
        }

        public virtual Task<string?> VfsToRealPathAsync(VfsPath path, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public virtual Task<VfsPath?> RealToVfsPathAsync(string path, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<VfsPath?>(null);
        }
    }
}
