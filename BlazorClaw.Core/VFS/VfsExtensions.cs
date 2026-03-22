namespace BlazorClaw.Core.VFS
{
    public static class VfsExtensions
    {
        public static Task<Stream> OpenAsync(this VfsFile file, FileMode mode, FileAccess access)
        {
            return file.VFS.OpenFileAsync(file.Path, mode, access);
        }

        public static Task DeleteAsync(this VfsEntity entity)
        {
            return entity.VFS.DeleteAsync(entity.Path);
        }

        public static IAsyncEnumerable<VfsPath> GetSubPathsAsync(this VfsDirectory directory)
        {
            return directory.VFS.GetSubPathsAsync(directory.Path);
        }

        public static async IAsyncEnumerable<VfsEntity> GetEntitiesAsync(this VfsDirectory directory)
        {
            var paths = directory.GetSubPathsAsync();
            await foreach (var p in paths)
            {
                yield return VfsEntity.Create(directory.VFS, p);
            }
        }

        public static async IAsyncEnumerable<VfsPath> GetSubPathsRecursiveAsync(this IVfsSystem fileSystem, VfsPath path)
        {
            if (!path.IsDirectory)
                throw new ArgumentException("The specified path is not a directory.");
            await foreach (var entity in fileSystem.GetSubPathsAsync(path))
            {
                yield return entity;
                if (entity.IsDirectory)
                    await foreach (var subentity in fileSystem.GetSubPathsRecursiveAsync(entity))
                        yield return subentity;
            }
        }

        public static async Task CreateDirectoryRecursiveAsync(this IVfsSystem fileSystem, VfsPath path)
        {
            if (!path.IsDirectory)
                throw new ArgumentException("The specified path is not a directory.");
            var currentDirectoryPath = VfsPath.Root;
            foreach (var dirName in path.GetDirectorySegments())
            {
                currentDirectoryPath = currentDirectoryPath.AppendDirectory(dirName);
                if (!await fileSystem.ExistsAsync(currentDirectoryPath))
                    await fileSystem.CreateDirectoryAsync(currentDirectoryPath);
            }
        }
    }
}