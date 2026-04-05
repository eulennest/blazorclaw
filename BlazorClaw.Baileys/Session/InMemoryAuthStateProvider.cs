using Baileys.Types;
using Baileys.Utils;

namespace Baileys.Session;

/// <summary>
/// An <see cref="IAuthStateProvider"/> that keeps credentials in process memory.
/// State is lost when the process exits — useful for short-lived sessions,
/// testing, or as a starting point when no persistence is needed.
/// </summary>
/// <remarks>
/// Thread-safe: a <see cref="SemaphoreSlim"/> guards every read/write.
/// </remarks>
public sealed class InMemoryAuthStateProvider : IAuthStateProvider
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private AuthenticationCreds? _creds;

    /// <summary>
    /// Initialises a new instance that starts with <see langword="null"/> credentials
    /// (a fresh set will be created on the first call to <see cref="LoadCredsAsync"/>).
    /// </summary>
    public InMemoryAuthStateProvider() { }

    /// <summary>
    /// Initialises a new instance pre-loaded with existing <paramref name="creds"/>.
    /// </summary>
    public InMemoryAuthStateProvider(AuthenticationCreds creds)
    {
        _creds = creds;
    }

    /// <inheritdoc/>
    public async Task<AuthenticationCreds> LoadCredsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _creds ??= AuthUtils.InitAuthCreds();
            return _creds;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task SaveCredsAsync(AuthenticationCreds creds, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _creds = creds;
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
            _creds = null;
        }
        finally
        {
            _lock.Release();
        }
    }
}
