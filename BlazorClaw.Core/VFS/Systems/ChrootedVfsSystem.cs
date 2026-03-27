namespace BlazorClaw.Core.VFS.Systems
{
    public class ChrootedVfsSystem(IVfsSystem fileSystem, VfsPath root) : VfsSystemWrapper(fileSystem)
    {
        protected VfsPath Root { get; private set; } = root;

        protected VfsPath AppendRoot(VfsPath path)
        {
            return Root.AppendPath(path);
        }

        protected VfsPath RemoveRoot(VfsPath path)
        {
            return path.RemoveParent(Root);
        }

        public override IAsyncEnumerable<VfsPath> GetSubPathsAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return base.GetSubPathsAsync(path, cancelationToken).Select(RemoveRoot);
        }

        public override IAsyncEnumerable<VfsPath> GetDirectorysAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return base.GetDirectorysAsync(path, cancelationToken).Select(RemoveRoot);
        }

        public override IAsyncEnumerable<VfsPath> GetFilesAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return base.GetFilesAsync(path, cancelationToken).Select(RemoveRoot);
        }

        public override Task<bool> ExistsAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return base.ExistsAsync(AppendRoot(path), cancelationToken);
        }

        public override Task CreateFileAsync(VfsPath path, Stream data, CancellationToken cancelationToken = default)
        {
            return base.CreateFileAsync(AppendRoot(path), data, cancelationToken);
        }
        public override Task<Stream> OpenFileAsync(VfsPath path, FileMode mode, FileAccess access, CancellationToken cancelationToken = default)
        {
            return base.OpenFileAsync(AppendRoot(path), mode, access, cancelationToken);
        }

        public override Task CreateDirectoryAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return base.CreateDirectoryAsync(AppendRoot(path), cancelationToken);
        }
        public override Task DeleteAsync(VfsPath path, CancellationToken cancellationToken = default)
        {
            return base.DeleteAsync(AppendRoot(path), cancellationToken);
        }
        public override Task DeleteRecursiveAsync(VfsPath path, CancellationToken cancellationToken = default)
        {
            return base.DeleteRecursiveAsync(AppendRoot(path), cancellationToken);
        }
    }
}