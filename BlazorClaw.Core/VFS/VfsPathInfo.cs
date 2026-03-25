namespace BlazorClaw.Core.VFS
{

    public class VfsPathInfo
    {
        public VfsPathInfo(VfsEntity entry) : this(entry, false, 0, DateTime.MinValue, DateTime.MinValue, false)
        { }
        public VfsPathInfo(VfsEntity entry, bool readOnly, long length, DateTime createTime, DateTime editTime, bool? exists = null)
        {
            Entry = entry;
            Path = entry.Path;
            Name = Path.EntityName;
            IsReadOnly = readOnly;
            Length = length;

            if (exists.HasValue) Exists = exists.Value;
            else if (length > 0) Exists = true;
            else if (length < 0) Exists = false;
            CreateTime = createTime;
            LastWriteTime = editTime;
        }

        public bool IsReadOnly { get; protected set; }

        public bool Exists { get; protected set; }

        public long Length { get; protected set; }

        public string Name { get; protected set; }

        public VfsEntity Entry { get; protected set; }

        public VfsPath Path { get; protected set; }

        public DateTime CreateTime { get; protected set; }

        public DateTime LastWriteTime { get; protected set; }

        public virtual Task<Stream> OpenAsync(FileMode mode, FileAccess access) => Entry.VFS.OpenFileAsync(Path, mode, access);
        public virtual Task<Stream> OpenReadAsync() => OpenAsync(FileMode.Open, FileAccess.Read);
        public virtual Task<Stream> OpenWriteAsync() => OpenAsync(FileMode.Create, FileAccess.Write);

        public virtual VfsPathInfo Clone(VfsEntity entity)
        {
            return new VfsPathInfo(entity, IsReadOnly, Length, CreateTime, LastWriteTime, Exists);
        }

        public virtual VfsPathInfo Clone(IVfsSystem sys, VfsPath path) => Clone(VfsEntity.Create(sys, path));

        public override string ToString()
        {
            return Path.ToString();
        }
    }

}
