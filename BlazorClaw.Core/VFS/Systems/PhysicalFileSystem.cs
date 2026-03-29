namespace BlazorClaw.Core.VFS.Systems
{
    public class PhysicalFileSystem : IVfsSystem
    {
        #region Internals
        public string PhysicalRoot { get; private set; }

        public PhysicalFileSystem(string physicalRoot)
        {
            if (!Path.IsPathRooted(physicalRoot))
                physicalRoot = Path.GetFullPath(physicalRoot);
            if (physicalRoot[^1] != Path.DirectorySeparatorChar)
                physicalRoot += Path.DirectorySeparatorChar;
            PhysicalRoot = physicalRoot;
        }

        protected string GetPhysicalPath(VfsPath path)
        {
            return Path.Combine(PhysicalRoot, path.ToString()[1..].Replace(VfsPath.DirectorySeparator, Path.DirectorySeparatorChar));
        }


        protected VfsPath GetVirtualFilePath(string physicalPath)
        {
            if (!physicalPath.StartsWith(PhysicalRoot, StringComparison.InvariantCultureIgnoreCase))
                throw new ArgumentException("The specified path is not member of the PhysicalRoot.", nameof(physicalPath));
            if (physicalPath[^1] != Path.DirectorySeparatorChar && Directory.Exists(physicalPath))
                physicalPath += Path.DirectorySeparatorChar;
            string virtualPath = VfsPath.DirectorySeparator + physicalPath[PhysicalRoot.Length..].Replace(Path.DirectorySeparatorChar, VfsPath.DirectorySeparator);
            return VfsPath.Parse(virtualPath);
        }

        protected VfsPath GetVirtualDirectoryPath(string physicalPath)
        {
            if (!physicalPath.StartsWith(PhysicalRoot, StringComparison.InvariantCultureIgnoreCase))
                throw new ArgumentException("The specified path is not member of the PhysicalRoot.", nameof(physicalPath));
            string virtualPath = VfsPath.DirectorySeparator + physicalPath[PhysicalRoot.Length..].Replace(Path.DirectorySeparatorChar, VfsPath.DirectorySeparator);
            if (virtualPath[^1] != VfsPath.DirectorySeparator)
                virtualPath += VfsPath.DirectorySeparator;
            return VfsPath.Parse(virtualPath);
        }

        #endregion
        public virtual Task<string?> VfsToRealPathAsync(VfsPath path, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(GetPhysicalPath(path));
        }
        public virtual Task<VfsPath?> RealToVfsPathAsync(string path, CancellationToken cancellationToken = default)
        {
            try
            {
                return Task.FromResult<VfsPath?>(GetVirtualFilePath(path));
            }
            catch (Exception)
            {
            }
            return Task.FromResult<VfsPath?>(null);
        }

        void IDisposable.Dispose()
        {
        }

        public IAsyncEnumerable<VfsPath> GetSubPathsAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            string physicalPath = GetPhysicalPath(path);
            return System.IO.Directory.EnumerateFileSystemEntries(physicalPath).ToAsyncEnumerable().Select(GetVirtualFilePath);
        }

        public IAsyncEnumerable<VfsPath> GetDirectorysAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            string physicalPath = GetPhysicalPath(path);
            return System.IO.Directory.EnumerateDirectories(physicalPath).ToAsyncEnumerable().Select(GetVirtualFilePath);
        }

        public IAsyncEnumerable<VfsPath> GetFilesAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            string physicalPath = GetPhysicalPath(path);
            return System.IO.Directory.EnumerateFiles(physicalPath).ToAsyncEnumerable().Select(GetVirtualFilePath);
        }

        public async ValueTask<VfsPathInfo> GetMetaInfoAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            var real = GetPhysicalPath(path);
            if (path.IsDirectory)
            {
                if (!Directory.Exists(real)) return new VfsPathInfo(VfsEntity.Create(this, path));
                var fi = new System.IO.DirectoryInfo(real);
                return new VfsPathInfo(VfsEntity.Create(this, path), false, 0, fi.CreationTime, fi.LastWriteTime);
            }
            else
            {
                if (!File.Exists(real)) return new VfsPathInfo(VfsEntity.Create(this, path));
                var fi = new System.IO.FileInfo(real);
                return new VfsPathInfo(VfsEntity.Create(this, path), fi.IsReadOnly, fi.Length, fi.CreationTime, fi.LastWriteTime);
            }
        }

        public ValueTask<bool> ExistsAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            return ValueTask.FromResult(path.IsFile ? System.IO.File.Exists(GetPhysicalPath(path)) : System.IO.Directory.Exists(GetPhysicalPath(path)));
        }

        public async Task CreateFileAsync(VfsPath path, Stream data, CancellationToken cancelationToken = default)
        {
            using var strm = File.Create(GetPhysicalPath(path));
            await data.CopyToAsync(strm, cancelationToken);
        }

        public Task<Stream> OpenFileAsync(VfsPath path, FileMode mode, FileAccess access, CancellationToken cancelationToken = default)
        {
            if (!path.IsFile)
                throw new ArgumentException("The specified path is not a file.", nameof(path));
            return Task.FromResult(System.IO.File.Open(GetPhysicalPath(path), mode, access) as Stream);
        }

        public Task CreateDirectoryAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            if (!path.IsDirectory)
                throw new ArgumentException("The specified path is not a directory.", nameof(path));
            System.IO.Directory.CreateDirectory(GetPhysicalPath(path));
            return Task.CompletedTask;
        }

        public Task DeleteAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            if (path.IsFile)
                System.IO.File.Delete(GetPhysicalPath(path));
            else
                System.IO.Directory.Delete(GetPhysicalPath(path));

            return Task.CompletedTask;
        }
        public Task DeleteRecursiveAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            if (path.IsFile)
                System.IO.File.Delete(GetPhysicalPath(path));
            else
                System.IO.Directory.Delete(GetPhysicalPath(path), true);

            return Task.CompletedTask;
        }
    }
}
