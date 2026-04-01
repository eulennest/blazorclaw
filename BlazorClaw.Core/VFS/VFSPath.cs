using System.Diagnostics;

namespace BlazorClaw.Core.VFS
{
    public struct VfsPath : IEquatable<VfsPath>, IComparable<VfsPath>
    {
        public static readonly char DirectorySeparator = '/';
        public static VfsPath Root { get; private set; }

        private readonly string _path;

        public readonly string Path
        {
            get { return _path ?? "/"; }
        }

        public bool IsDirectory
        {
            get { return Path[^1] == DirectorySeparator; }
        }

        public bool IsFile
        {
            get { return !IsDirectory; }
        }

        public bool IsRoot
        {
            get { return Path.Length == 1; }
        }

        public string? EntityName
        {
            get
            {
                string name = Path;
                if (IsRoot)
                    return null;
                int endOfName = name.Length;
                if (IsDirectory)
                    endOfName--;
                int startOfName = name.LastIndexOf(DirectorySeparator, endOfName - 1, endOfName) + 1;
                return name[startOfName..endOfName];
            }
        }

        public VfsPath ParentPath
        {
            get
            {
                string parentPath = Path;
                if (IsRoot)
                    throw new InvalidOperationException("There is no parent of root.");
                int lookaheadCount = parentPath.Length;
                if (IsDirectory)
                    lookaheadCount--;
                int index = parentPath.LastIndexOf(DirectorySeparator, lookaheadCount - 1, lookaheadCount);
                Debug.Assert(index >= 0);
                parentPath = parentPath[..(index + 1)];
                return new VfsPath(parentPath);
            }
        }

        static VfsPath()
        {
            Root = new VfsPath(DirectorySeparator.ToString());
        }

        private VfsPath(string path)
        {
            _path = path;
        }

        public static bool IsRooted(string? s)
        {
            return s?.Length > 0 && s[0] == DirectorySeparator;
        }

        public static VfsPath Parse(string s)
        {
            ArgumentNullException.ThrowIfNull(s);
            if (s == DirectorySeparator.ToString() + DirectorySeparator) s = DirectorySeparator.ToString();
            if (!IsRooted(s))
                throw new ParseException(s, "Path is not rooted");
            if (s.Contains(string.Concat(DirectorySeparator, DirectorySeparator)))
                throw new ParseException(s, "Path contains double directory-separators.");
            // Check for segments that contain only dots and are longer than 2 (..., ...., etc.)
            // Single dot (.) and double dots (..) should not appear in absolute paths
            // Use Parse(cwd, path) for relative path resolution with . and ..
            var segments = s.Split(new[] { DirectorySeparator }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                if (segment.All(c => c == '.') && segment.Length >= 1)
                    throw new ParseException(s, $"Path contains invalid segment: \"{segment}\" - use Parse(cwd, path) for relative path resolution");
            }

            return new VfsPath(s);
        }

        /// <summary>
        /// Parse a path relative to a current working directory.
        /// Supports absolute paths (starting with /), relative paths, ./ and ..
        /// Absolute paths: cwd is set to root internally
        /// Example: Parse("/home/", "../etc/config") → "/etc/config"
        /// Example: Parse("/home/", "/etc/config") → "/etc/config"
        /// </summary>
        public static VfsPath Parse(VfsPath cwd, string relativePath, VfsPathParseMode mode = VfsPathParseMode.Auto)
        {
            ArgumentNullException.ThrowIfNull(relativePath);

            // If absolute path, reset cwd to root
            if (IsRooted(relativePath))
            {
                cwd = VfsPath.Root;
                // Remove leading / for processing
                relativePath = relativePath[1..];

                // If nothing left, return root
                if (string.IsNullOrEmpty(relativePath))
                    return cwd;
            }

            // Start from cwd (must be a directory)
            if (!cwd.IsDirectory)
                throw new ArgumentException("Current working directory must be a directory", nameof(cwd));
            char[] allSeps = [DirectorySeparator, '\\', '/'];

            // Normalize and split path
            var segments = relativePath
                .Trim()
                .Split(allSeps, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (segments.Count == 0)
                return cwd;

            var result = cwd;

            string? filename = null;
            if (mode == VfsPathParseMode.File || (mode == VfsPathParseMode.Auto && !allSeps.Contains(relativePath.Last())))
            {
                filename = segments[^1];
                segments.RemoveAt(segments.Count - 1);
            }

            // Process all segments except the last (which could be a file)
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i].Trim();

                if (string.IsNullOrWhiteSpace(segment) || segment == ".")
                {
                    // Current directory - do nothing
                    continue;
                }
                else if (segment == "..")
                {
                    // Parent directory
                    if (result.IsRoot)
                    {
                        // Already at root, can't go up further
                        continue;
                    }
                    result = result.ParentPath;
                }
                else if (segment.Contains(DirectorySeparator))
                {
                    // Invalid: segment contains separator
                    throw new ParseException(relativePath, $"Invalid path segment: {segment}");
                }
                else if (segment.All(c => c == '.') && segment.Length > 2)
                {
                    // Segment with 3+ dots (..., ...., etc.)
                    throw new ParseException(relativePath, $"Invalid path segment: \"{segment}\"");
                }
                else
                {
                    // Regular directory name
                    result = result.AppendDirectory(segment);
                }
            }

            if (!string.IsNullOrWhiteSpace(filename))
            {
                // Process last segment (could be file or directory)
                if (filename == ".")
                {
                    // Just cwd, already at result
                }
                else if (filename == "..")
                {
                    // Parent directory
                    if (!result.IsRoot)
                        result = result.ParentPath;
                }
                else if (filename.All(c => c == '.'))
                {
                    // Segment with only dots and more than 2 (..., ...., etc.)
                    throw new ParseException(relativePath, $"Invalid path segment: \"{filename}\"");
                }
                else
                {
                    result = result.AppendFile(filename);
                }
            }

            return result;
        }

        public static VfsPath ParseDirectory(string s)
        {
            return VfsPath.Parse(VfsPath.DirectorySeparator + s.Trim().Trim(VfsPath.DirectorySeparator) + VfsPath.DirectorySeparator);
        }

        public VfsPath AppendPath(string relativePath)
        {
            if (IsRooted(relativePath))
                throw new ArgumentException("The specified path should be relative.", nameof(relativePath));
            if (!IsDirectory)
                throw new InvalidOperationException("This " + nameof(VfsPath) + " is not a directory.");
            return new VfsPath(Path + relativePath);
        }

        public VfsPath AppendPath(VfsPath path)
        {
            if (!IsDirectory)
                throw new InvalidOperationException("This " + nameof(VfsPath) + " is not a directory.");
            return new VfsPath(Path + path.Path[1..]);
        }

        public VfsPath AppendDirectory(string directoryName)
        {
            if (directoryName.Contains(DirectorySeparator.ToString()))
                throw new ArgumentException("The specified name includes directory-separator(s).", nameof(directoryName));
            if (!IsDirectory)
                throw new InvalidOperationException("The specified FileSystemPath is not a directory.");
            return new VfsPath(Path + directoryName + DirectorySeparator);
        }

        public VfsPath AppendFile(string fileName)
        {
            if (fileName.Contains(DirectorySeparator.ToString()))
                throw new ArgumentException("The specified name includes directory-separator(s).", nameof(fileName));
            if (!IsDirectory)
                throw new InvalidOperationException("The specified FileSystemPath is not a directory.");
            return new VfsPath(Path + fileName);
        }

        public bool IsParentOf(VfsPath path)
        {
            return IsDirectory && Path.Length != path.Path.Length && path.Path.StartsWith(Path);
        }


        public readonly bool IsChildOf(VfsPath path)
        {
            return path.IsParentOf(this);
        }


        public VfsPath RemoveParent(VfsPath parent)
        {
            if (!parent.IsDirectory)
                throw new ArgumentException("The specified path can not be the parent of this path: it is not a directory.");
            if (!Path.StartsWith(parent.Path))
                throw new ArgumentException("The specified path is not a parent of this path.");
            return new VfsPath(Path[(parent.Path.Length - 1)..]);
        }

        public string MakeRelative(VfsPath parent)
        {
            if (!parent.IsDirectory)
                throw new ArgumentException("The specified path can not be the parent of this path: it is not a directory.");
            if (!Path.StartsWith(parent.Path))
                return Path;
            if (Path.Equals(parent.Path))
                return "./";
            return Path[(parent.Path.Length)..];
        }

        public VfsPath RemoveChild(VfsPath child)
        {
            if (!Path.EndsWith(child.Path))
                throw new ArgumentException("The specified path is not a child of this path.");
            return new VfsPath(Path[..(Path.Length - child.Path.Length + 1)]);
        }


        public string GetExtension()
        {
            if (!IsFile)
                throw new ArgumentException("The specified FileSystemPath is not a file.");
            var name = EntityName;
            int extensionIndex = name?.LastIndexOf('.') ?? -1;
            return extensionIndex < 0 ? "" : name![extensionIndex..];
        }


        public VfsPath ChangeExtension(string extension)
        {
            if (!IsFile)
                throw new ArgumentException("The specified FileSystemPath is not a file.");
            string name = EntityName;
            int extensionIndex = name.LastIndexOf('.');
            if (extensionIndex >= 0)
                return ParentPath.AppendFile(name[..extensionIndex] + extension);
            return VfsPath.Parse(Path + extension);
        }

        public string[] GetDirectorySegments()
        {
            VfsPath path = this;
            if (IsFile)
                path = path.ParentPath;
            var segments = new LinkedList<string>();
            while (!path.IsRoot)
            {
                segments.AddFirst(path.EntityName);
                path = path.ParentPath;
            }
            return [.. segments];
        }


        public int CompareTo(VfsPath other)
        {
            var dire = GetDirectory().Path.CompareTo(other.GetDirectory().Path);
            if (dire == 0)
            {
                if (IsFile && other.IsFile) return (EntityName ?? string.Empty).CompareTo(other.EntityName);
                if (IsDirectory && other.IsFile) return -1;
                if (IsFile && other.IsDirectory) return 1;
            }
            return dire;
        }

        public VfsPath GetDirectory() => IsFile ? ParentPath : this;

        public override string ToString()
        {
            return Path;
        }


        public override bool Equals(object? obj)
        {
            if (obj is string str)
                return Path.Equals(str);
            if (obj is VfsPath path)
                return Equals(path);
            return false;
        }


        public bool Equals(VfsPath other)
        {
            return other.Path.Equals(Path);
        }


        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }

        public static bool operator ==(VfsPath pathA, VfsPath pathB)
        {
            return pathA.Equals(pathB);
        }

        public static bool operator !=(VfsPath pathA, VfsPath pathB)
        {
            return !(pathA == pathB);
        }
    }
    public enum VfsPathParseMode
    {
        Auto = 0,
        File = 1,
        Directory = 2
    }

    public class InvalidPathException(string path) : Exception($"Invalid path '{path}'")
    {
        public string Path { get; } = path;
    }
}