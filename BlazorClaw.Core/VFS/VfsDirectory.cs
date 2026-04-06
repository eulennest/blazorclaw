namespace BlazorClaw.Core.VFS
{
    public class VfsDirectory : VfsEntity, IEquatable<VfsDirectory>
    {
        public VfsDirectory(IVfsSystem fileSystem, VfsPath path) : base(fileSystem, path)
        {
            if (!path.IsDirectory)
                throw new ArgumentException("The specified path is no directory.", nameof(path));
        }

        public bool Equals(VfsDirectory? other)
        {
            if(other is VfsEntity entry) return base.Equals(entry);
            return false;
        }
    }
}