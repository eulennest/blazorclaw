namespace BlazorClaw.Core.VFS
{
    public class VfsFile : VfsEntity, IEquatable<VfsFile>
    {
        public VfsFile(IVfsSystem fileSystem, VfsPath path) : base(fileSystem, path)
        {
            if (!path.IsFile)
                throw new ArgumentException("The specified path is no file.", nameof(path));
        }

        public bool Equals(VfsFile? other)
        {
            return ((IEquatable<VfsEntity>)this).Equals(other);
        }
    }
}