using Baileys.Types;
using System.Collections.Concurrent;

namespace Baileys.Session;

/// <summary>
/// An <see cref="ISignalKeyStore"/> that stores all Signal-protocol keys in
/// process memory.  State is lost when the process exits — ideal for tests,
/// ephemeral sessions, or as an inner store wrapped by a caching layer.
/// </summary>
/// <remarks>
/// Thread-safe: all mutations go through <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </remarks>
public sealed class InMemorySignalKeyStore : ISignalKeyStore
{
    // Outer key: signal data type (e.g. "pre-key", "session")
    // Inner key: item id
    // Value:     raw JSON bytes
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte[]>> _store = new();

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<string, byte[]?>> GetAsync(
        string type,
        IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default)
    {
        var bucket = _store.GetValueOrDefault(type);
        var result = new Dictionary<string, byte[]?>(ids.Count);
        foreach (var id in ids)
        {
            result[id] = bucket is not null && bucket.TryGetValue(id, out var value) ? value : null;
        }

        return Task.FromResult<IReadOnlyDictionary<string, byte[]?>>(result);
    }

    /// <inheritdoc/>
    public Task SetAsync(
        string type,
        IReadOnlyDictionary<string, byte[]?> values,
        CancellationToken cancellationToken = default)
    {
        foreach (var (id, value) in values)
        {
            if (value is null)
            {
                if (_store.TryGetValue(type, out var bucket))
                    bucket.TryRemove(id, out _);
            }
            else
            {
                var bucket = _store.GetOrAdd(type, _ => new ConcurrentDictionary<string, byte[]>());
                bucket[id] = value;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _store.Clear();
        return Task.CompletedTask;
    }
}
