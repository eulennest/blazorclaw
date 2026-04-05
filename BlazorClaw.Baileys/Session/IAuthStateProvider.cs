using Baileys.Types;

namespace Baileys.Session;

/// <summary>
/// Abstraction for loading and persisting a Baileys authentication session.
/// Mirrors the TypeScript <c>AuthState</c> concept (<c>{ creds, keys }</c>)
/// with async, cancellation-friendly methods to support any backing store.
/// </summary>
/// <remarks>
/// Implementations should be registered as <em>scoped</em> or <em>singleton</em>
/// services depending on whether your application manages one or multiple
/// WhatsApp sessions.
///
/// <para>Built-in implementations:</para>
/// <list type="bullet">
///   <item><see cref="InMemoryAuthStateProvider"/> – stores state in process memory (default)</item>
///   <item><see cref="FileAuthStateProvider"/> – persists to a JSON file on disk</item>
/// </list>
/// </remarks>
public interface IAuthStateProvider
{
    /// <summary>
    /// Loads the <see cref="AuthenticationCreds"/> for this session.
    /// If no persisted credentials exist a fresh set must be created (see
    /// <see cref="Utils.AuthUtils.InitAuthCreds"/>).
    /// </summary>
    Task<AuthenticationCreds> LoadCredsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the current <see cref="AuthenticationCreds"/> so that the session
    /// can be resumed after a restart.
    /// </summary>
    Task SaveCredsAsync(AuthenticationCreds creds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all persisted state for this session.
    /// After calling this, the next <see cref="LoadCredsAsync"/> will return fresh credentials.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
