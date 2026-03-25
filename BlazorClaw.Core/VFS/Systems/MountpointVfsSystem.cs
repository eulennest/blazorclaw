using BlazorClaw.Core.Utils;
using System.Runtime.CompilerServices;

namespace BlazorClaw.Core.VFS.Systems
{
    public class MountpointVfsSystem : IVfsSystem
    {
        public class MountEntry(IVfsSystem vfs, bool hidden = false)
        {
            public IVfsSystem VFS { get; } = vfs;
            public bool Hidden { get; } = hidden;
        }
        public ICollection<KeyValuePair<VfsPath, MountEntry>> Mounts { get; private set; }

        public MountpointVfsSystem(IEnumerable<KeyValuePair<VfsPath, MountEntry>> mounts)
        {
            Mounts = new SortedList<VfsPath, MountEntry>(new InverseComparer<VfsPath>(Comparer<VfsPath>.Default));
            foreach (var mount in mounts)
                Mounts.Add(mount);
        }

        public MountpointVfsSystem(params KeyValuePair<VfsPath, MountEntry>[] mounts)
            : this((IEnumerable<KeyValuePair<VfsPath, MountEntry>>)mounts)
        {
        }

        public void AddMountpoint(VfsPath path, IVfsSystem vfs, bool hidden = false)
        {
            Mounts.Add(new KeyValuePair<VfsPath, MountEntry>(path, new MountEntry(vfs, hidden)));
        }

        protected KeyValuePair<VfsPath, MountEntry>? Get(VfsPath path)
        {
            var t = Mounts.FirstOrDefault(pair => pair.Key == path || pair.Key.IsParentOf(path));
            return t.Value == null ? null : t;
        }

        public void Dispose()
        {
            foreach (var mount in Mounts.Select(p => p.Value))
                mount.VFS.Dispose();
        }

        protected IEnumerable<VfsPath> GetMountPoints(VfsPath path)
        {
            return [.. Mounts.Where(o => !o.Value.Hidden && !o.Key.IsRoot && path.Equals(o.Key.ParentPath)).Select(o => o.Key).OrderBy(o => o)];
        }

        public async Task<VfsPathInfo> GetMetaInfoAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            var pair = Get(path);
            if (pair != null)
            {
                var fi = await pair.Value.Value.VFS.GetMetaInfoAsync(path.RemoveParent(pair.Value.Key), cancelationToken);
                if (fi != null) return fi.Clone(this, path);
            }
            return new VfsPathInfo(VfsEntity.Create(this, path));
        }

        public async IAsyncEnumerable<VfsPath> GetSubPathsAsync(VfsPath path, [EnumeratorCancellation] CancellationToken cancelationToken = default)
        {
            var pair = Get(path);
            if (pair != null)
            {
                await foreach (var item in pair.Value.Value.VFS.GetSubPathsAsync(path.IsRoot ? path : path.RemoveParent(pair.Value.Key), cancelationToken))
                {
                    yield return pair.Value.Key.AppendPath(item);
                }
            }
            foreach (var item in GetMountPoints(path))
            {
                yield return item;
            }
        }

        public async IAsyncEnumerable<VfsPath> GetDirectorysAsync(VfsPath path, [EnumeratorCancellation] CancellationToken cancelationToken = default)
        {
            var pair = Get(path);
            if (pair != null)
            {
                await foreach (var item in pair.Value.Value.VFS.GetDirectorysAsync(path.IsRoot ? path : path.RemoveParent(pair.Value.Key), cancelationToken))
                {
                    yield return pair.Value.Key.AppendPath(item);
                }
            }
            foreach (var item in GetMountPoints(path))
            {
                yield return item;
            }
        }

        public async IAsyncEnumerable<VfsPath> GetFilesAsync(VfsPath path, [EnumeratorCancellation] CancellationToken cancelationToken = default)
        {
            var pair = Get(path);
            if (pair != null)
            {
                await foreach (var item in pair.Value.Value.VFS.GetFilesAsync(path.IsRoot ? path : path.RemoveParent(pair.Value.Key), cancelationToken))
                {
                    yield return pair.Value.Key.AppendPath(item);
                }
            }
        }

        public Task<bool> ExistsAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            if (path.IsRoot) return Task.FromResult(true);
            var pair = Get(path) ?? throw new FileNotFoundException("mountpoint not found", path.ToString());
            return pair.Value.VFS.ExistsAsync(path.RemoveParent(pair.Key), cancelationToken);
        }

        public Task CreateFileAsync(VfsPath path, Stream data, CancellationToken cancelationToken = default)
        {
            var pair = Get(path) ?? throw new FileNotFoundException("mountpoint not found", path.ToString());
            return pair.Value.VFS.CreateFileAsync(path.RemoveParent(pair.Key), data, cancelationToken);
        }

        public Task<Stream> OpenFileAsync(VfsPath path, FileMode mode, FileAccess access, CancellationToken cancelationToken = default)
        {
            var pair = Get(path) ?? throw new FileNotFoundException("mountpoint not found", path.ToString());
            return pair.Value.VFS.OpenFileAsync(path.RemoveParent(pair.Key), mode, access, cancelationToken);
        }

        public Task CreateDirectoryAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            var pair = Get(path) ?? throw new FileNotFoundException("mountpoint not found", path.ToString());
            return pair.Value.VFS.CreateDirectoryAsync(path.RemoveParent(pair.Key), cancelationToken);
        }

        public Task DeleteAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            var pair = Get(path) ?? throw new FileNotFoundException("mountpoint not found", path.ToString());
            return pair.Value.VFS.DeleteAsync(path.RemoveParent(pair.Key), cancelationToken);
        }
    }
}