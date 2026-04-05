using Baileys.Types;

namespace Baileys.Session;

/// <summary>
/// An <see cref="ISignalKeyStore"/> that persists each Signal-protocol key as
/// a separate file inside a directory, mirroring the TypeScript
/// <c>useMultiFileAuthState</c> helper from
/// <c>Utils/use-multi-file-auth-state.ts</c>.
/// </summary>
/// <remarks>
/// <para>
/// Files are named <c>{type}-{sanitized-id}</c> where <c>/</c> is replaced
/// by <c>__</c> and <c>:</c> by <c>-</c>, exactly as in the TypeScript helper.
/// All remaining OS-specific invalid filename characters are replaced with
/// <c>_</c>. The resolved path is always verified to stay within the store
/// directory to prevent path traversal.
/// </para>
/// <para>
/// Thread-safe per instance: a <see cref="SemaphoreSlim"/> serialises all
/// file I/O for a given <see cref="DirectorySignalKeyStore"/> instance.
/// Multiple instances (or processes) pointing at the same directory are not
/// co-ordinated by this lock.
/// </para>
/// </remarks>
public sealed class DirectorySignalKeyStore : ISignalKeyStore
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>The directory in which key files are stored.</summary>
    public string Directory { get; }

    /// <summary>
    /// Initialises a new <see cref="DirectorySignalKeyStore"/> that stores
    /// keys under <paramref name="directory"/>.
    /// The directory is created automatically when it does not exist.
    /// </summary>
    public DirectorySignalKeyStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory = directory;
        System.IO.Directory.CreateDirectory(directory);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, byte[]?>> GetAsync(
        string type,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, byte[]?>(ids.Count);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var id in ids)
            {
                var path = GetFilePath(type, id);
                result[id] = File.Exists(path)
                    ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)
                    : null;
            }
        }
        finally
        {
            _lock.Release();
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task SetAsync(
        string type,
        IReadOnlyDictionary<string, byte[]?> values,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var (id, value) in values)
            {
                var path = GetFilePath(type, id);
                if (value is null)
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                else
                {
                    await File.WriteAllBytesAsync(path, value, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (System.IO.Directory.Exists(Directory))
            {
                foreach (var file in System.IO.Directory.GetFiles(Directory))
                {
                    if (IsKeyFile(Path.GetFileName(file)))
                        File.Delete(file);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    // All known Signal data type prefixes — used to identify key files.
    private static readonly string[] KnownTypePrefixes = [
        SignalDataTypes.PreKey            + "-",
        SignalDataTypes.Session           + "-",
        SignalDataTypes.SenderKey         + "-",
        SignalDataTypes.SenderKeyMemory   + "-",
        SignalDataTypes.AppStateSyncKey   + "-",
        SignalDataTypes.AppStateSyncVersion + "-",
        SignalDataTypes.LidMapping        + "-",
        SignalDataTypes.DeviceList        + "-",
        SignalDataTypes.TcToken           + "-",
        SignalDataTypes.IdentityKey       + "-",
    ];

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="fileName"/> is a
    /// Signal key file created by this store (i.e., its name starts with a
    /// known <see cref="SignalDataTypes"/> prefix followed by a dash).
    /// </summary>
    private static bool IsKeyFile(string fileName)
    {
        foreach (var prefix in KnownTypePrefixes)
        {
            if (fileName.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the file path for a given signal data type + id, applying the
    /// same filename sanitisation as the TypeScript helper:
    /// <c>/</c> → <c>__</c>, <c>:</c> → <c>-</c>, all remaining
    /// OS-specific invalid filename characters → <c>_</c>.
    /// The resolved path is always verified to be inside
    /// <see cref="Directory"/>.
    /// </summary>
    public string GetFilePath(string type, string id)
    {
        // TypeScript-compatible replacements
        var sanitizedId = id.Replace("/", "__", StringComparison.Ordinal)
                            .Replace(":", "-", StringComparison.Ordinal);

        // Replace any remaining invalid filename characters (including '\' on
        // Windows) and explicit directory separators.
        var chars = sanitizedId.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(InvalidChars, chars[i]) >= 0
                || chars[i] == Path.DirectorySeparatorChar
                || chars[i] == Path.AltDirectorySeparatorChar)
            {
                chars[i] = '_';
            }
        }
        sanitizedId = new string(chars);

        var candidate = Path.GetFullPath(Path.Combine(Directory, $"{type}-{sanitizedId}"));
        var dirRoot   = Path.GetFullPath(Directory);

        // Guard against any residual path traversal: the candidate must be a
        // direct child of dirRoot, not equal to it and not outside it.
        var relative = Path.GetRelativePath(dirRoot, candidate);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException(
                "Key id resolves to a path outside the store directory.");
        }

        return candidate;
    }
}
