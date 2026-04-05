using System.Text.Json;
using System.Text.Json.Serialization;
using Baileys.Types;
using Baileys.Utils;

namespace Baileys.Session;

/// <summary>
/// An <see cref="IAuthStateProvider"/> that persists the full authentication
/// state — credentials <em>and</em> Signal-protocol keys — inside a single
/// directory on the local file system.
/// </summary>
/// <remarks>
/// <para>
/// This is the direct .NET equivalent of the TypeScript
/// <c>useMultiFileAuthState(folder)</c> helper:
/// <list type="bullet">
///   <item>Credentials are stored in <c>{directory}/creds.json</c>.</item>
///   <item>
///     Each Signal key is stored in its own file, following the naming
///     convention used by <see cref="DirectorySignalKeyStore"/>.
///   </item>
/// </list>
/// </para>
/// <para>
/// The <see cref="Keys"/> property exposes the underlying
/// <see cref="DirectorySignalKeyStore"/> so callers can supply it to whatever
/// socket/client implementation they build on top of Baileys.
/// </para>
/// <para>
/// Thread-safe: a <see cref="SemaphoreSlim"/> serialises credentials file I/O;
/// signal-key I/O is serialised by <see cref="DirectorySignalKeyStore"/>'s own
/// internal lock.
/// </para>
/// </remarks>
public sealed class DirectoryAuthStateProvider : IAuthStateProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>The directory used to store all session state.</summary>
    public string Directory { get; }

    /// <summary>
    /// The Signal-protocol key store backed by this directory.
    /// Pass this to your socket/client implementation via
    /// <see cref="AuthenticationState.Keys"/>.
    /// </summary>
    public DirectorySignalKeyStore Keys { get; }

    /// <summary>
    /// Initialises a new <see cref="DirectoryAuthStateProvider"/> that reads
    /// and writes all session state under <paramref name="directory"/>.
    /// The directory is created automatically when it does not exist.
    /// </summary>
    public DirectoryAuthStateProvider(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        System.IO.Directory.CreateDirectory(directory);
        Directory = directory;
        Keys = new DirectorySignalKeyStore(directory);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  IAuthStateProvider
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<AuthenticationCreds> LoadCredsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadCredsInternalAsync(cancellationToken).ConfigureAwait(false);
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
            await SaveCredsInternalAsync(creds, cancellationToken).ConfigureAwait(false);
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
            var credsFile = CredsFilePath;
            if (File.Exists(credsFile))
                File.Delete(credsFile);
        }
        finally
        {
            _lock.Release();
        }

        // Clear signal keys (uses its own lock)
        await Keys.ClearAsync(cancellationToken).ConfigureAwait(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Full auth-state helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the full <see cref="AuthenticationState"/> (credentials +
    /// Signal keys) from this directory.
    /// </summary>
    /// <remarks>
    /// This is the primary entry-point for integrating Baileys, mirroring the
    /// TypeScript <c>useMultiFileAuthState</c> pattern:
    /// <code>
    /// var state = await provider.LoadAuthStateAsync();
    /// // state.Creds  — use when building the connection
    /// // state.Keys   — use as the signal key store
    /// </code>
    /// </remarks>
    public async Task<AuthenticationState> LoadAuthStateAsync(
        CancellationToken cancellationToken = default)
    {
        var creds = await LoadCredsAsync(cancellationToken).ConfigureAwait(false);
        return new AuthenticationState { Creds = creds, Keys = Keys };
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private string CredsFilePath => Path.Combine(Directory, "creds.json");

    private async Task<AuthenticationCreds> LoadCredsInternalAsync(CancellationToken ct)
    {
        if (!File.Exists(CredsFilePath))
            return AuthUtils.InitAuthCreds();

        await using var stream = File.OpenRead(CredsFilePath);
        var dto = await JsonSerializer
            .DeserializeAsync<AuthCredsDto>(stream, JsonOptions, ct)
            .ConfigureAwait(false);

        return dto?.ToAuthenticationCreds() ?? AuthUtils.InitAuthCreds();
    }

    private async Task SaveCredsInternalAsync(AuthenticationCreds creds, CancellationToken ct)
    {
        var dto      = AuthCredsDto.FromAuthenticationCreds(creds);
        var tempPath = Path.Combine(Directory, $"creds.json.{Path.GetRandomFileName()}.tmp");
        try
        {
            await using (var stream = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, ct).ConfigureAwait(false);
            }
            // Atomic-replace: on POSIX systems File.Move maps to rename(2) which
            // is atomic. On Windows it is best-effort (not a single kernel op),
            // but still far safer than overwriting creds.json directly.
            // Concurrent writes from multiple instances are not co-ordinated.
            File.Move(tempPath, CredsFilePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    //  JSON data transfer objects
    // ────────────────────────────────────────────────────────────────────────

    private sealed class AuthCredsDto
    {
        public KeyPairDto? NoiseKey { get; set; }
        public KeyPairDto? PairingEphemeralKeyPair { get; set; }
        public string? AdvSecretKey { get; set; }
        public KeyPairDto? SignedIdentityKey { get; set; }
        public SignedKeyPairDto? SignedPreKey { get; set; }
        public int RegistrationId { get; set; }
        public int FirstUnuploadedPreKeyId { get; set; }
        public int NextPreKeyId { get; set; }
        public int AccountSyncCounter { get; set; }
        public bool Registered { get; set; }
        public string? PairingCode { get; set; }
        public string? LastPropHash { get; set; }
        public string? RoutingInfo { get; set; }
        public long? LastAccountSyncTimestamp { get; set; }
        public string? Platform { get; set; }
        public AccountSettingsDto? AccountSettings { get; set; }

        public AuthenticationCreds ToAuthenticationCreds() => new()
        {
            NoiseKey                 = NoiseKey?.ToKeyPair()                ?? new KeyPair([], []),
            PairingEphemeralKeyPair  = PairingEphemeralKeyPair?.ToKeyPair() ?? new KeyPair([], []),
            AdvSecretKey             = AdvSecretKey                         ?? string.Empty,
            SignedIdentityKey        = SignedIdentityKey?.ToKeyPair()       ?? new KeyPair([], []),
            SignedPreKey             = SignedPreKey?.ToSignedKeyPair()      ?? new SignedKeyPair(new KeyPair([], []), [], 0),
            RegistrationId           = RegistrationId,
            FirstUnuploadedPreKeyId  = FirstUnuploadedPreKeyId,
            NextPreKeyId             = NextPreKeyId,
            AccountSyncCounter       = AccountSyncCounter,
            Registered               = Registered,
            PairingCode              = PairingCode,
            LastPropHash             = LastPropHash,
            RoutingInfo              = RoutingInfo is null ? null : Convert.FromBase64String(RoutingInfo),
            LastAccountSyncTimestamp = LastAccountSyncTimestamp,
            Platform                 = Platform,
            AccountSettings          = AccountSettings?.ToAccountSettings() ?? new AccountSettings()
        };

        public static AuthCredsDto FromAuthenticationCreds(AuthenticationCreds c) => new()
        {
            NoiseKey                 = KeyPairDto.FromKeyPair(c.NoiseKey),
            PairingEphemeralKeyPair  = KeyPairDto.FromKeyPair(c.PairingEphemeralKeyPair),
            AdvSecretKey             = c.AdvSecretKey,
            SignedIdentityKey        = KeyPairDto.FromKeyPair(c.SignedIdentityKey),
            SignedPreKey             = SignedKeyPairDto.FromSignedKeyPair(c.SignedPreKey),
            RegistrationId           = c.RegistrationId,
            FirstUnuploadedPreKeyId  = c.FirstUnuploadedPreKeyId,
            NextPreKeyId             = c.NextPreKeyId,
            AccountSyncCounter       = c.AccountSyncCounter,
            Registered               = c.Registered,
            PairingCode              = c.PairingCode,
            LastPropHash             = c.LastPropHash,
            RoutingInfo              = c.RoutingInfo is null ? null : Convert.ToBase64String(c.RoutingInfo),
            LastAccountSyncTimestamp = c.LastAccountSyncTimestamp,
            Platform                 = c.Platform,
            AccountSettings          = AccountSettingsDto.FromAccountSettings(c.AccountSettings)
        };
    }

    private sealed class KeyPairDto
    {
        public string? Public { get; set; }
        public string? Private { get; set; }

        public KeyPair ToKeyPair() => new(
            Public:  Public  is null ? [] : Convert.FromBase64String(Public),
            Private: Private is null ? [] : Convert.FromBase64String(Private));

        public static KeyPairDto FromKeyPair(KeyPair kp) => new()
        {
            Public  = Convert.ToBase64String(kp.Public),
            Private = Convert.ToBase64String(kp.Private)
        };
    }

    private sealed class SignedKeyPairDto
    {
        public KeyPairDto? KeyPair { get; set; }
        public string? Signature { get; set; }
        public int KeyId { get; set; }
        public long? TimestampSeconds { get; set; }

        public SignedKeyPair ToSignedKeyPair() => new(
            KeyPair:          KeyPair?.ToKeyPair() ?? new KeyPair([], []),
            Signature:        Signature is null ? [] : Convert.FromBase64String(Signature),
            KeyId:            KeyId,
            TimestampSeconds: TimestampSeconds);

        public static SignedKeyPairDto FromSignedKeyPair(SignedKeyPair skp) => new()
        {
            KeyPair          = KeyPairDto.FromKeyPair(skp.KeyPair),
            Signature        = Convert.ToBase64String(skp.Signature),
            KeyId            = skp.KeyId,
            TimestampSeconds = skp.TimestampSeconds
        };
    }

    private sealed class AccountSettingsDto
    {
        public bool UnarchiveChats { get; set; }
        public int? DefaultEphemeralExpiration { get; set; }
        public long? DefaultEphemeralSettingTimestamp { get; set; }

        public AccountSettings ToAccountSettings() => new()
        {
            UnarchiveChats                   = UnarchiveChats,
            DefaultEphemeralExpiration       = DefaultEphemeralExpiration,
            DefaultEphemeralSettingTimestamp = DefaultEphemeralSettingTimestamp
        };

        public static AccountSettingsDto FromAccountSettings(AccountSettings s) => new()
        {
            UnarchiveChats                   = s.UnarchiveChats,
            DefaultEphemeralExpiration       = s.DefaultEphemeralExpiration,
            DefaultEphemeralSettingTimestamp = s.DefaultEphemeralSettingTimestamp
        };
    }
}
