using System.Runtime.CompilerServices;

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


        public static async Task WriteAllTextAsync(this IVfsSystem fileSystem, VfsPath path, string content, CancellationToken cancellationToken = default)
        {
            if (path.IsDirectory)
                throw new ArgumentException("The specified path is not a file.");

            using var stream = await fileSystem.OpenFileAsync(path, FileMode.Create, FileAccess.Write, cancellationToken);
            using var reader = new StreamWriter(stream);
            await reader.WriteAsync(content);
        }

        public static async Task<string> ReadAllTextAsync(this IVfsSystem fileSystem, VfsPath path, CancellationToken cancellationToken = default)
        {
            if (path.IsDirectory)
                throw new ArgumentException("The specified path is not a file.");

            using var stream = await fileSystem.OpenFileAsync(path, FileMode.Open, FileAccess.Read, cancellationToken);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        public static async IAsyncEnumerable<string> ReadLinesAsync(this IVfsSystem fileSystem, VfsPath path, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (path.IsDirectory)
                throw new ArgumentException("The specified path is not a file.");

            using var stream = await fileSystem.OpenFileAsync(path, FileMode.Open, FileAccess.Read, cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is not null)
                    yield return line;
            }
        }

        public static async Task<string[]> ReadAllLinesAsync(this IVfsSystem fileSystem, VfsPath path, CancellationToken cancellationToken = default)
        {
            return await ReadLinesAsync(fileSystem, path, cancellationToken).ToArrayAsync(cancellationToken);
        }
    }
}