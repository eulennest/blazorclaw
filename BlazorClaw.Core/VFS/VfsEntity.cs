namespace BlazorClaw.Core.VFS
{
    public class VfsEntity(IVfsSystem fileSystem, VfsPath path) : IEquatable<VfsEntity>
    {
        public IVfsSystem VFS { get; private set; } = fileSystem;
        public VfsPath Path { get; private set; } = path;
        public string Name { get { return Path.EntityName ?? string.Empty; } }

        public override bool Equals(object? obj)
        {
            return obj is VfsEntity other && Equals(other);
        }

        public override int GetHashCode() => HashCode.Combine(VFS, Path);

        public virtual bool Equals(VfsEntity? other)
        {
            return VFS.Equals(other?.VFS) && Path.Equals(other.Path);
        }

        public static VfsEntity Create(IVfsSystem fileSystem, VfsPath path)
        {
            if (path.IsFile)
                return new VfsFile(fileSystem, path);
            else
                return new VfsDirectory(fileSystem, path);
        }

        public override string ToString()
        {
            return Path.ToString();
        }
    }
}