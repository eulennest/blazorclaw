namespace BlazorClaw.Core.VFS
{
    public class VfsEntity : IEquatable<VfsEntity>
    {
        public IVfsSystem VFS { get; private set; }
        public VfsPath Path { get; private set; }
        public string Name { get { return Path.EntityName; } }

        public VfsEntity(IVfsSystem fileSystem, VfsPath path)
        {
            VFS = fileSystem;
            Path = path;
        }

        public override bool Equals(object obj)
        {
            return obj is VfsEntity other && Equals(other);
        }

        public override int GetHashCode() => HashCode.Combine(VFS, Path);

        bool IEquatable<VfsEntity>.Equals(VfsEntity other)
        {
            return VFS.Equals(other.VFS) && Path.Equals(other.Path);
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