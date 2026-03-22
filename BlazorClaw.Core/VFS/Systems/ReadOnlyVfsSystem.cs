namespace BlazorClaw.Core.VFS.Systems
{
    public class ReadOnlyFileSystem(IVfsSystem parent) : VfsSystemWrapper(parent)
    {
        public override Task<Stream> OpenFileAsync(VfsPath path, FileMode mode, FileAccess access, CancellationToken cancelationToken = default)
        {
            if (access != FileAccess.Read)
                throw new InvalidOperationException("This is a read-only filesystem.");
            return base.OpenFileAsync(path, mode, access, cancelationToken);
        }

        public override Task CreateFileAsync(VfsPath path, Stream data, CancellationToken cancelationToken = default)
        {
            throw new InvalidOperationException("This is a read-only filesystem.");
        }

        public override Task CreateDirectoryAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            throw new InvalidOperationException("This is a read-only filesystem.");
        }

        public override Task DeleteAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            throw new InvalidOperationException("This is a read-only filesystem.");
        }
    }
}