using Baileys.Session;
using Baileys.Types;

namespace Baileys.Extensions;

/// <summary>
/// Extension methods for <see cref="IAuthStateProvider"/> that add helpers for
/// building a full <see cref="AuthenticationState"/>, mirroring the TypeScript
/// <c>useMultiFileAuthState</c> pattern where the loaded state bundles both
/// credentials and the Signal-protocol key store.
/// </summary>
public static class AuthStateExtensions
{
    /// <summary>
    /// Loads the <see cref="AuthenticationCreds"/> from <paramref name="provider"/>
    /// and bundles them with <paramref name="keys"/> into an
    /// <see cref="AuthenticationState"/> ready for use by a Baileys connection.
    /// </summary>
    /// <param name="provider">The source of the authentication credentials.</param>
    /// <param name="keys">
    /// The Signal-protocol key store to associate with the loaded credentials.
    /// When <see langword="null"/> a new <see cref="InMemorySignalKeyStore"/> is
    /// created automatically, which is suitable for short-lived or test sessions.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// An <see cref="AuthenticationState"/> containing the loaded credentials and
    /// the supplied (or newly created) key store.
    /// </returns>
    /// <example>
    /// <code>
    /// // In-memory key store (keys lost on restart):
    /// var state = await provider.LoadAuthStateAsync();
    ///
    /// // Persistent key store (keys survive restarts):
    /// var keys = new DirectorySignalKeyStore("baileys_keys");
    /// var state = await provider.LoadAuthStateAsync(keys);
    ///
    /// // With DirectoryAuthStateProvider (creds + keys in one directory):
    /// var dirProvider = new DirectoryAuthStateProvider("baileys_auth");
    /// var state = await dirProvider.LoadAuthStateAsync();
    /// </code>
    /// </example>
    public static async Task<AuthenticationState> LoadAuthStateAsync(
        this IAuthStateProvider provider,
        ISignalKeyStore? keys = null,
        CancellationToken cancellationToken = default)
    {
        // DirectoryAuthStateProvider already bundles its own DirectorySignalKeyStore,
        // so delegate to its typed overload when available.
        if (provider is DirectoryAuthStateProvider dirProvider && keys is null)
            return await dirProvider.LoadAuthStateAsync(cancellationToken).ConfigureAwait(false);

        var creds = await provider.LoadCredsAsync(cancellationToken).ConfigureAwait(false);
        return new AuthenticationState
        {
            Creds = creds,
            Keys = keys ?? new InMemorySignalKeyStore()
        };
    }
}
