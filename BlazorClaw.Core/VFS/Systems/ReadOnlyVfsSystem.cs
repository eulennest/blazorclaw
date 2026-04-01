namespace BlazorClaw.Core.VFS.Systems
{
    public class ReadOnlyFileSystem(IVfsSystem parent) : VfsSystemWrapper(parent)
    {
        public override Task<Stream> OpenFileAsync(VfsPath path, FileMode mode, FileAccess access, CancellationToken cancellationToken = default)
        {
            if (access != FileAccess.Read)
                throw new InvalidOperationException("This is a read-only filesystem.");
            return base.OpenFileAsync(path, mode, access, cancellationToken);
        }

        public override Task CreateFileAsync(VfsPath path, Stream data, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("This is a read-only filesystem.");
        }

        public override Task CreateDirectoryAsync(VfsPath path, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("This is a read-only filesystem.");
        }

        public override Task DeleteAsync(VfsPath path, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("This is a read-only filesystem.");
        }
        public override Task DeleteRecursiveAsync(VfsPath path, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("This is a read-only filesystem.");
        }
        public override ValueTask MoveAsync(VfsPath pathFrom, VfsPath pathTo, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("This is a read-only filesystem.");
        }
    }
}