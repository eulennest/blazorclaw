using Baileys.Types;
using Baileys.Utils;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Baileys.Session;

/// <summary>
/// An <see cref="IAuthStateProvider"/> that persists credentials as a JSON file
/// on the local file system.  The file is created automatically on the first save.
/// </summary>
/// <remarks>
/// <para>
/// This mirrors the TypeScript <c>useMultiFileAuthState(folder)</c> helper for
/// single-device sessions — all credential data lives in one JSON file at
/// <see cref="FilePath"/>.
/// </para>
/// <para>
/// Thread-safe: a <see cref="SemaphoreSlim"/> serialises concurrent access.
/// </para>
/// </remarks>
public sealed class FileAuthStateProvider : IAuthStateProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>The absolute or relative path of the JSON credentials file.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Initialises a new <see cref="FileAuthStateProvider"/> that reads/writes
    /// credentials from <paramref name="filePath"/>.
    /// The directory must already exist; the file will be created if it is absent.
    /// </summary>
    /// <param name="filePath">
    /// Path to the credentials JSON file, e.g. <c>"baileys_auth.json"</c> or
    /// <c>"/var/data/session/creds.json"</c>.
    /// </param>
    public FileAuthStateProvider(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;
    }

    /// <inheritdoc/>
    public async Task<AuthenticationCreds> LoadCredsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(FilePath))
                return AuthUtils.InitAuthCreds();

            await using var stream = File.OpenRead(FilePath);
            var dto = await JsonSerializer.DeserializeAsync<AuthCredsDto>(stream, JsonOptions, cancellationToken)
                          .ConfigureAwait(false);

            return dto is null ? AuthUtils.InitAuthCreds() : dto.ToAuthenticationCreds();
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
            var dto = AuthCredsDto.FromAuthenticationCreds(creds);
            await using var stream = File.Open(FilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, cancellationToken)
                                .ConfigureAwait(false);
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
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    //  JSON data transfer objects — flat, serialisable representations of
    //  AuthenticationCreds and its nested types.
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
            NoiseKey = NoiseKey?.ToKeyPair() ?? new KeyPair([], []),
            PairingEphemeralKeyPair = PairingEphemeralKeyPair?.ToKeyPair() ?? new KeyPair([], []),
            AdvSecretKey = AdvSecretKey ?? string.Empty,
            SignedIdentityKey = SignedIdentityKey?.ToKeyPair() ?? new KeyPair([], []),
            SignedPreKey = SignedPreKey?.ToSignedKeyPair() ?? new SignedKeyPair(new KeyPair([], []), [], 0),
            RegistrationId = RegistrationId,
            FirstUnuploadedPreKeyId = FirstUnuploadedPreKeyId,
            NextPreKeyId = NextPreKeyId,
            AccountSyncCounter = AccountSyncCounter,
            Registered = Registered,
            PairingCode = PairingCode,
            LastPropHash = LastPropHash,
            RoutingInfo = RoutingInfo is null ? null : Convert.FromBase64String(RoutingInfo),
            LastAccountSyncTimestamp = LastAccountSyncTimestamp,
            Platform = Platform,
            AccountSettings = AccountSettings?.ToAccountSettings() ?? new AccountSettings()
        };

        public static AuthCredsDto FromAuthenticationCreds(AuthenticationCreds c) => new()
        {
            NoiseKey = KeyPairDto.FromKeyPair(c.NoiseKey),
            PairingEphemeralKeyPair = KeyPairDto.FromKeyPair(c.PairingEphemeralKeyPair),
            AdvSecretKey = c.AdvSecretKey,
            SignedIdentityKey = KeyPairDto.FromKeyPair(c.SignedIdentityKey),
            SignedPreKey = SignedKeyPairDto.FromSignedKeyPair(c.SignedPreKey),
            RegistrationId = c.RegistrationId,
            FirstUnuploadedPreKeyId = c.FirstUnuploadedPreKeyId,
            NextPreKeyId = c.NextPreKeyId,
            AccountSyncCounter = c.AccountSyncCounter,
            Registered = c.Registered,
            PairingCode = c.PairingCode,
            LastPropHash = c.LastPropHash,
            RoutingInfo = c.RoutingInfo is null ? null : Convert.ToBase64String(c.RoutingInfo),
            LastAccountSyncTimestamp = c.LastAccountSyncTimestamp,
            Platform = c.Platform,
            AccountSettings = AccountSettingsDto.FromAccountSettings(c.AccountSettings)
        };
    }

    private sealed class KeyPairDto
    {
        public string? Public { get; set; }
        public string? Private { get; set; }

        public KeyPair ToKeyPair() => new(
            Public: Public is null ? [] : Convert.FromBase64String(Public),
            Private: Private is null ? [] : Convert.FromBase64String(Private));

        public static KeyPairDto FromKeyPair(KeyPair kp) => new()
        {
            Public = Convert.ToBase64String(kp.Public),
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
            KeyPair: KeyPair?.ToKeyPair() ?? new KeyPair([], []),
            Signature: Signature is null ? [] : Convert.FromBase64String(Signature),
            KeyId: KeyId,
            TimestampSeconds: TimestampSeconds);

        public static SignedKeyPairDto FromSignedKeyPair(SignedKeyPair skp) => new()
        {
            KeyPair = KeyPairDto.FromKeyPair(skp.KeyPair),
            Signature = Convert.ToBase64String(skp.Signature),
            KeyId = skp.KeyId,
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
            UnarchiveChats = UnarchiveChats,
            DefaultEphemeralExpiration = DefaultEphemeralExpiration,
            DefaultEphemeralSettingTimestamp = DefaultEphemeralSettingTimestamp
        };

        public static AccountSettingsDto FromAccountSettings(AccountSettings s) => new()
        {
            UnarchiveChats = s.UnarchiveChats,
            DefaultEphemeralExpiration = s.DefaultEphemeralExpiration,
            DefaultEphemeralSettingTimestamp = s.DefaultEphemeralSettingTimestamp
        };
    }
}
