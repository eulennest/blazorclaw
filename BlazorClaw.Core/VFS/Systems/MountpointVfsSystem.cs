using BlazorClaw.Core.Utils;
using System.Runtime.CompilerServices;

namespace BlazorClaw.Core.VFS.Systems
{
    public class MountpointVfsSystem : IVfsSystem
    {
        public ICollection<KeyValuePair<VfsPath, IVfsSystem>> Mounts { get; private set; }

        public MountpointVfsSystem(IEnumerable<KeyValuePair<VfsPath, IVfsSystem>> mounts)
        {
            Mounts = new SortedList<VfsPath, IVfsSystem>(new InverseComparer<VfsPath>(Comparer<VfsPath>.Default));
            foreach (var mount in mounts)
                Mounts.Add(mount);
        }

        public MountpointVfsSystem(params KeyValuePair<VfsPath, IVfsSystem>[] mounts)
            : this((IEnumerable<KeyValuePair<VfsPath, IVfsSystem>>)mounts)
        {
        }

        public void AddMountpoint(VfsPath path, IVfsSystem vfs)
        {
            Mounts.Add(new KeyValuePair<VfsPath, IVfsSystem>(path, vfs));
        }

        protected KeyValuePair<VfsPath, IVfsSystem>? Get(VfsPath path)
        {
            var t = Mounts.FirstOrDefault(pair => pair.Key == path || pair.Key.IsParentOf(path));
            return t;
        }

        public void Dispose()
        {
            foreach (var mount in Mounts.Select(p => p.Value))
                mount.Dispose();
        }

        protected IEnumerable<VfsPath> GetMountPoints(VfsPath path)
        {
            return [.. Mounts.Where(o => !o.Key.IsRoot && path.Equals(o.Key.ParentPath)).Select(o => o.Key).OrderBy(o => o)];
        }

        public async Task<VfsPathInfo> GetMetaInfoAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            var pair = Get(path);
            if (pair != null)
            {
                var fi = await pair.Value.Value.GetMetaInfoAsync(path.RemoveParent(pair.Value.Key), cancelationToken);
                if (fi != null) return fi.Clone(this, path);
            }
            return new VfsPathInfo(VfsEntity.Create(this, path));
        }

        public async IAsyncEnumerable<VfsPath> GetSubPathsAsync(VfsPath path, [EnumeratorCancellation] CancellationToken cancelationToken = default)
        {
            var pair = Get(path);
            if (pair != null)
            {
                await foreach (var item in pair.Value.Value.GetSubPathsAsync(path.IsRoot ? path : path.RemoveParent(pair.Value.Key), cancelationToken))
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
                await foreach (var item in pair.Value.Value.GetDirectorysAsync(path.IsRoot ? path : path.RemoveParent(pair.Value.Key), cancelationToken))
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
                await foreach (var item in pair.Value.Value.GetFilesAsync(path.IsRoot ? path : path.RemoveParent(pair.Value.Key), cancelationToken))
                {
                    yield return pair.Value.Key.AppendPath(item);
                }
            }
        }

        public Task<bool> ExistsAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            if (path.IsRoot) return Task.FromResult(true);
            var pair = Get(path) ?? throw new FileNotFoundException("mountpoint not found", path.ToString());
            return pair.Value.ExistsAsync(path.RemoveParent(pair.Key), cancelationToken);
        }

        public Task CreateFileAsync(VfsPath path, Stream data, CancellationToken cancelationToken = default)
        {
            var pair = Get(path) ?? throw new FileNotFoundException("mountpoint not found", path.ToString());
            return pair.Value.CreateFileAsync(path.RemoveParent(pair.Key), data, cancelationToken);
        }

        public Task<Stream> OpenFileAsync(VfsPath path, FileMode mode, FileAccess access, CancellationToken cancelationToken = default)
        {
            var pair = Get(path) ?? throw new FileNotFoundException("mountpoint not found", path.ToString());
            return pair.Value.OpenFileAsync(path.RemoveParent(pair.Key), mode, access, cancelationToken);
        }

        public Task CreateDirectoryAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            var pair = Get(path) ?? throw new FileNotFoundException("mountpoint not found", path.ToString());
            return pair.Value.CreateDirectoryAsync(path.RemoveParent(pair.Key), cancelationToken);
        }

        public Task DeleteAsync(VfsPath path, CancellationToken cancelationToken = default)
        {
            var pair = Get(path) ?? throw new FileNotFoundException("mountpoint not found", path.ToString());
            return pair.Value.DeleteAsync(path.RemoveParent(pair.Key), cancelationToken);
        }
    }
}