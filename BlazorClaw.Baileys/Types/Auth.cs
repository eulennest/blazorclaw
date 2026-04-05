namespace Baileys.Types;

/// <summary>Reasons a WhatsApp WebSocket connection may be disconnected.</summary>
public enum DisconnectReason
{
    ConnectionClosed = 428,
    ConnectionLost = 408,
    ConnectionReplaced = 440,
    TimedOut = 408,
    LoggedOut = 401,
    BadSession = 500,
    RestartRequired = 515,
    MultideviceMismatch = 411,
    Forbidden = 403,
    UnavailableService = 503
}

/// <summary>WhatsApp version triplet [major, minor, patch].</summary>
public sealed record WaVersion(int Major, int Minor, int Patch)
{
    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

/// <summary>Browser description used during connection [platform, browser, version].</summary>
public sealed record WaBrowserDescription(string Platform, string Browser, string Version);

/// <summary>A public/private key-pair (raw bytes).</summary>
public sealed record KeyPair(byte[] Public, byte[] Private);

/// <summary>A signed pre-key used in the Signal protocol.</summary>
public sealed record SignedKeyPair(KeyPair KeyPair, byte[] Signature, int KeyId, long? TimestampSeconds = null);

/// <summary>Signal protocol address (name = JID, deviceId).</summary>
public sealed record ProtocolAddress(string Name, int DeviceId);

/// <summary>A Signal identity consisting of an address and the identifier key bytes.</summary>
public sealed record SignalIdentity(ProtocolAddress Identifier, byte[] IdentifierKey);

/// <summary>LID ↔ Phone-number mapping.</summary>
public sealed record LidMapping(string PhoneNumber, string Lid);

/// <summary>State used for app-state LT-Hash verification.</summary>
public sealed class LtHashState
{
    public int Version { get; set; }
    public byte[] Hash { get; set; } = [];
    public Dictionary<string, byte[]> IndexValueMap { get; set; } = new();
}

/// <summary>Minimal Signal credentials.</summary>
public sealed record SignalCreds(
    KeyPair SignedIdentityKey,
    SignedKeyPair SignedPreKey,
    int RegistrationId);

/// <summary>Account-level settings.</summary>
public sealed class AccountSettings
{
    /// <summary>Whether to unarchive chats when a new message arrives.</summary>
    public bool UnarchiveChats { get; set; }
    public int? DefaultEphemeralExpiration { get; set; }
    public long? DefaultEphemeralSettingTimestamp { get; set; }
}

/// <summary>
/// Full authentication state combining credentials and the Signal-protocol
/// key store, mirroring the TypeScript <c>AuthenticationState</c> type from
/// <c>Types/Auth.ts</c>.
/// </summary>
public sealed class AuthenticationState
{
    /// <summary>The WhatsApp authentication credentials.</summary>
    public required AuthenticationCreds Creds { get; init; }

    /// <summary>The Signal-protocol key store for this session.</summary>
    public required ISignalKeyStore Keys { get; init; }
}

/// <summary>Full authentication credentials including Signal creds and session metadata.</summary>
public sealed class AuthenticationCreds
{
    public required KeyPair NoiseKey { get; set; }
    public required KeyPair PairingEphemeralKeyPair { get; set; }
    public required string AdvSecretKey { get; set; }
    public required KeyPair SignedIdentityKey { get; set; }
    public required SignedKeyPair SignedPreKey { get; set; }
    public int RegistrationId { get; set; }
    public int FirstUnuploadedPreKeyId { get; set; }
    public int NextPreKeyId { get; set; }
    public int AccountSyncCounter { get; set; }
    public bool Registered { get; set; }
    public string? PairingCode { get; set; }
    public string? LastPropHash { get; set; }
    public byte[]? RoutingInfo { get; set; }
    public long? LastAccountSyncTimestamp { get; set; }
    public string? Platform { get; set; }
    public AccountSettings AccountSettings { get; set; } = new();
}
