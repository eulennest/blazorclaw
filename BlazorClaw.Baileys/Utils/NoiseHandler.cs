using Baileys.Crypto;
using Baileys.Defaults;
using Baileys.Types;

namespace Baileys.Utils;

/// <summary>
/// Implements the WhatsApp Noise_XX_25519_AESGCM_SHA256 handshake protocol,
/// mirroring <c>Utils/noise-handler.ts</c>.
///
/// The Noise handler wraps an underlying transport (WebSocket), performing
/// the 3-message Noise XX handshake and then acting as an encrypted transport
/// using AES-256-GCM with monotonically increasing counters.
/// </summary>
public sealed class NoiseHandler
{
    private const int IvLength = 12;

    // ── Handshake state ────────────────────────────────────────
    private byte[] _hash;
    private byte[] _salt;
    private byte[] _encKey;
    private byte[] _decKey;
    private int _Counter;
    private bool _transportEstablished;

    private readonly KeyPair _keyPair;
    private readonly ILogger _logger;
    private TransportState? _transport;

    // ── Intro header (prepended to the first frame sent) ──────
    private readonly byte[] _introHeader;

    public NoiseHandler(KeyPair keyPair, ILogger? logger = null, byte[]? routingInfo = null)
    {
        _keyPair = keyPair;
        _logger = (logger ?? NullLogger.Instance).Child(
            new Dictionary<string, object> { ["class"] = "ns" });

        // Initialise hash/salt/encKey/decKey from the noise mode string
        var modeBytes = System.Text.Encoding.ASCII.GetBytes(BaileysDefaults.NoiseMode);
        _hash = modeBytes.Length == 32 ? modeBytes : Crypto.Sha256(modeBytes);
        _salt = _hash;
        _encKey = _hash;
        _decKey = _hash;

        // Build the intro header
        var noiseHeader = BaileysDefaults.NoiseWaHeader;
        if (routingInfo is { Length: > 0 })
        {
            _introHeader = new byte[7 + routingInfo.Length + noiseHeader.Length];
            _introHeader[0] = (byte)'E';
            _introHeader[1] = (byte)'D';
            _introHeader[2] = 0;
            _introHeader[3] = 1;
            _introHeader[4] = (byte)(routingInfo.Length >> 16);
            _introHeader[5] = (byte)((routingInfo.Length >> 8) & 0xFF);
            _introHeader[6] = (byte)(routingInfo.Length & 0xFF);
            routingInfo.CopyTo(_introHeader, 7);
            noiseHeader.CopyTo(_introHeader, 7 + routingInfo.Length);
        }
        else
        {
            _introHeader = [.. noiseHeader];
        }
        Authenticate(noiseHeader);
        Authenticate(_keyPair.Public);
    }

    // ──────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────

    /// <summary>Returns the intro header to prepend to the first outgoing frame.</summary>
    public ReadOnlySpan<byte> IntroHeader => _introHeader;

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> and returns a framed payload for
    /// transmission over the WebSocket.  During the handshake this also updates
    /// the hash/salt chain.
    /// </summary>
    public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
    {
        if (_transport != null)
            return _transport.Encrypt(plaintext);

        var iv = GenerateIv(_Counter++);
        var ciphertext = Crypto.AesEncryptGcm(plaintext, _encKey, iv, _hash);
        Authenticate(ciphertext);
        return ciphertext;
    }

    /// <summary>
    /// Decrypts a frame received from the WebSocket.
    /// </summary>
    public byte[] Decrypt(ReadOnlySpan<byte> ciphertext)
    {
        if (_transport != null)
            return _transport.Decrypt(ciphertext);

        // TypeScript Baileys: decrypt first, THEN authenticate(ciphertext)!
        var iv = GenerateIv(_Counter++);
        var plaintext = Crypto.AesDecryptGcm(ciphertext, _decKey, iv, _hash);
        Authenticate(ciphertext);
        return plaintext;

    }

    public byte[] ProcessHandshake(Proto.HandshakeMessage serverHello)
    {
        if (serverHello?.ServerHello == null)
            throw new ArgumentException("Invalid ServerHello message");

        var empBytes = serverHello.ServerHello.Ephemeral.ToByteArray();
        // TypeScript Baileys: FIRST authenticate(ephemeral), THEN mixIntoKey!
        Authenticate(empBytes);
        var sharedSecret = Curve25519Utils.CalculateAgreement(_keyPair.Private, empBytes);
        MixIntoKey(sharedSecret);

        // Static entschlüsseln
        var decStaticContent = Decrypt(serverHello.ServerHello.Static.Span);
        MixIntoKey(Curve25519Utils.CalculateAgreement(_keyPair.Private, decStaticContent));

        // Payload entschlüsseln (wie Static)
        var decPayloadContent = Decrypt(serverHello.ServerHello.Payload.Span);
        // Hash-Kette wird in Decrypt() automatisch aktualisiert (via Authenticate)

        // ClientFinish vorbereiten
        var keyEnc = Encrypt(_keyPair.Public);
        MixIntoKey(Curve25519Utils.CalculateAgreement(_keyPair.Private, empBytes));

        return keyEnc;
    }

    /// <summary>
    /// Finalises the handshake: derives the transport enc/dec keys from the current
    /// salt using HKDF and switches to transport-mode encryption/decryption.
    ///
    /// In the full Noise XX protocol the two peers use swapped enc/dec roles.
    /// Both keys are derived deterministically from the shared hash chain, so both
    /// peers independently arrive at the same key pair.
    /// </summary>
    public void Finish()
    {
        // Expand the current salt into 64 bytes via HKDF.
        // bytes  0–31 → encKey,  bytes 32–63 → decKey
        var expanded = Crypto.Hkdf([], 64, _salt, []);
        _transport = new TransportState(expanded[..32], expanded[32..]);
        _logger.Trace("noise handshake complete");
    }

    /// <summary>Mixes the provided data into the handshake hash and key chain.</summary>
    public void MixHash(ReadOnlySpan<byte> data) => Authenticate(data);


    // ──────────────────────────────────────────────────────────
    //  Internals
    // ──────────────────────────────────────────────────────────

    private void Authenticate(ReadOnlySpan<byte> data)
    {
        if (_transport != null) return;
        var combined = new byte[_hash.Length + data.Length];
        _hash.CopyTo(combined, 0);
        data.CopyTo(combined.AsSpan(_hash.Length));
        _logger.Debug($"[Authenticate] oldHash = {ToHex(_hash)}");
        _hash = Crypto.Sha256(combined);
        _logger.Debug($"[Authenticate] newHash = {ToHex(_hash)}");
    }


    private static byte[] GenerateIv(int counter)
    {
        var iv = new byte[IvLength];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(iv.AsSpan(8), counter);
        return iv;
    }

    public void MixIntoKey(ReadOnlySpan<byte> data)
    {
        // TypeScript Baileys uses HKDF, NOT HMAC!
        // const [write, read] = localHKDF(data)
        // salt = write
        // encKey = read
        // decKey = read
        var expanded = Crypto.Hkdf(data.ToArray(), 64, _salt, Array.Empty<byte>());
        _salt = expanded[..32];
        _encKey = expanded[32..];
        _decKey = _encKey; // Same as encKey during handshake!
        _Counter = 0;
    }

    public byte[] GetSalt() => (byte[])_salt.Clone();
    public byte[] GetEncKey() => (byte[])_encKey.Clone();
    public byte[] GetDecKey() => (byte[])_decKey.Clone();
    public byte[] GetHash() => (byte[])_hash.Clone();

    public void SetEncKey(byte[] key) => _encKey = (byte[])key.Clone();
    public void SetDecKey(byte[] key) => _decKey = (byte[])key.Clone();
    public void SetCounter(int counter) => _Counter = counter;

    private static string ToHex(byte[] data)
    {
        return BitConverter.ToString(data).Replace("-", "");
    }
}

public class TransportState(byte[] encKey, byte[] decKey)
{
    private const int IvLength = 12; // Standard für GCM

    private long _readCounter;
    private long _writeCounter;
    private readonly byte[] _iv = new byte[IvLength];

    public byte[] EncKey { get; } = encKey ?? throw new ArgumentNullException(nameof(encKey));
    public byte[] DecKey { get; } = decKey ?? throw new ArgumentNullException(nameof(decKey));

    public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
    {
        var c = _writeCounter++;
        UpdateIv(c);
        return Crypto.AesEncryptGcm(plaintext, EncKey, _iv, []);
    }

    public byte[] Decrypt(ReadOnlySpan<byte> ciphertext)
    {
        var c = _readCounter++;
        UpdateIv(c);
        return Crypto.AesDecryptGcm(ciphertext, DecKey, _iv, []);
    }

    private void UpdateIv(long counter)
    {
        _iv[8] = (byte)((counter >> 24) & 0xFF);
        _iv[9] = (byte)((counter >> 16) & 0xFF);
        _iv[10] = (byte)((counter >> 8) & 0xFF);
        _iv[11] = (byte)(counter & 0xFF);
    }
}
