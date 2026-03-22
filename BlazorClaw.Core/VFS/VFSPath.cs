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
            return new VfsPath(s);
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
}