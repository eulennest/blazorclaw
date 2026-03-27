namespace BlazorClaw.Core.VFS.Systems
{
    public class VfsSystemWrapper(IVfsSystem parent) : IVfsSystem
    {
        protected IVfsSystem Parent { get; private set; } = parent;

        public virtual void Dispose()
        {
            Parent.Dispose();
        }

        public virtual IAsyncEnumerable<VfsPath> GetSubPathsAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return Parent.GetSubPathsAsync(path, cancelationToken);
        }

        public virtual IAsyncEnumerable<VfsPath> GetDirectorysAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return Parent.GetDirectorysAsync(path, cancelationToken);
        }

        public virtual IAsyncEnumerable<VfsPath> GetFilesAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return Parent.GetFilesAsync(path, cancelationToken);
        }

        public virtual Task<VfsPathInfo> GetMetaInfoAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return Parent.GetMetaInfoAsync(path, cancelationToken);
        }

        public virtual Task<bool> ExistsAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return Parent.ExistsAsync(path, cancelationToken);
        }

        public virtual Task CreateFileAsync(VfsPath path, Stream data, CancellationToken cancelationToken = default)
        {
            return Parent.CreateFileAsync(path, data, cancelationToken);
        }

        public virtual Task<Stream> OpenFileAsync(VfsPath path, FileMode mode, FileAccess access, CancellationToken cancelationToken = default)
        {
            return Parent.OpenFileAsync(path, mode, access, cancelationToken);
        }

        public virtual Task CreateDirectoryAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return Parent.CreateDirectoryAsync(path, cancelationToken);
        }

        public virtual Task DeleteAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return Parent.DeleteAsync(path, cancelationToken);
        }

        public virtual Task DeleteRecursiveAsync(VfsPath path, CancellationToken cancellationToken = default)
        {
            return Parent.DeleteRecursiveAsync(path, cancellationToken);
        }
        public virtual Task<string?> VfsToRealPathAsync(VfsPath path, CancellationToken cancellationToken = default)
        {
            return Parent.VfsToRealPathAsync(path, cancellationToken);
        }
        public virtual Task<VfsPath?> RealToVfsPathAsync(string path, CancellationToken cancellationToken = default)
        {
            return Parent.RealToVfsPathAsync(path, cancellationToken);
        }
    }
}