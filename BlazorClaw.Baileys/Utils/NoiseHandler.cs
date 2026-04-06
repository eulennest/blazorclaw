using Baileys.Crypto;
using Baileys.Defaults;
using Baileys.Types;
using System.Security.Cryptography;
using static Proto.HandshakeMessage.Types;
using System;
using System.Text;

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
    private int _writeCounter;
    private int _readCounter;
    private bool _transportEstablished;

    private readonly KeyPair _keyPair;
    private readonly ILogger _logger;

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
        if (!_transportEstablished)
        {
            var iv = GenerateIv(_writeCounter++);
            var ciphertext = Crypto.AesEncryptGcm(plaintext, _encKey, iv, ReadOnlySpan<byte>.Empty);
            Authenticate(ciphertext); // TypeScript: authenticate(result) aka ciphertext!
            return ciphertext;
        }
        else
        {
            return EncryptTransport(plaintext);
        }
    }

    /// <summary>
    /// Decrypts a frame received from the WebSocket.
    /// </summary>
    public byte[] Decrypt(ReadOnlySpan<byte> ciphertext)
    {
        if (!_transportEstablished)
        {
            // TypeScript Baileys: decrypt first, THEN authenticate(ciphertext)!
            var iv = GenerateIv(_readCounter++);
            Console.WriteLine($"[Decrypt] iv={BitConverter.ToString(iv.ToArray()).Replace("-", "")} (counter={_readCounter-1})");
            var plaintext = Crypto.AesDecryptGcm(ciphertext, _decKey, iv, _hash);
            Authenticate(ciphertext);
            return plaintext;
        }
        else
        {
            return DecryptTransport(ciphertext);
        }
    }

    public byte[] ProcessHandshake(Proto.HandshakeMessage serverHello)
    {
        var empBytes = serverHello.ServerHello.Ephemeral.ToByteArray();
        Console.WriteLine($"[ProcessHandshake] Ephemeral: {BitConverter.ToString(empBytes).Replace("-", "")}");
        Console.WriteLine($"[ProcessHandshake] Hash before Authenticate: {BitConverter.ToString(_hash).Replace("-", "")}");
        // TypeScript Baileys: FIRST authenticate(ephemeral), THEN mixIntoKey!
        Authenticate(empBytes);
        Console.WriteLine($"[ProcessHandshake] Hash after Authenticate(ephemeral): {BitConverter.ToString(_hash).Replace("-", "")}");
        var sharedSecret = Curve25519Utils.CalculateAgreement(_keyPair.Private, empBytes);
        Console.WriteLine($"[ProcessHandshake] SharedSecret: {BitConverter.ToString(sharedSecret).Replace("-", "")}");
        var expanded = MixIntoKey(sharedSecret);
        // TypeScript Baileys: mixIntoKey does NOT update hash! We must do it manually!
        Authenticate(expanded);
        Console.WriteLine($"[ProcessHandshake] After MixIntoKey: decKey={BitConverter.ToString(_decKey).Replace("-", "")}");
        Console.WriteLine($"[ProcessHandshake] Hash after MixIntoKey: {BitConverter.ToString(_hash).Replace("-", "")}");

        Console.WriteLine($"[ProcessHandshake] Decrypt: ciphertext={BitConverter.ToString(serverHello!.ServerHello.Static.Span.ToArray()).Replace("-", "")}");
        Console.WriteLine($"[ProcessHandshake] Decrypt: ciphertext.Length={serverHello!.ServerHello.Static.Span.Length}");
        Console.WriteLine($"[ProcessHandshake] Decrypt: decKey={BitConverter.ToString(_decKey).Replace("-", "")}");
        Console.WriteLine($"[ProcessHandshake] Decrypt: AAD={BitConverter.ToString(_hash).Replace("-", "")}");
        var decStaticContent = Decrypt(serverHello!.ServerHello.Static.Span);
        MixIntoKey(Curve25519Utils.CalculateAgreement(_keyPair.Private, decStaticContent));

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
        var expanded = Crypto.Hkdf(Array.Empty<byte>(), 64, _salt, Array.Empty<byte>());
        _encKey = expanded[..32];
        _decKey = expanded[32..];
        _writeCounter = 0;
        _readCounter = 0;
        _transportEstablished = true;
        _logger.Trace("noise handshake complete");
    }

    /// <summary>Mixes the provided data into the handshake hash and key chain.</summary>
    public void MixHash(ReadOnlySpan<byte> data) => Authenticate(data);

    /// <summary>
    /// Decrypts using the enc key (same key used by <see cref="Encrypt"/> in
    /// transport mode).  This enables symmetric loopback tests where send and
    /// receive use the same key.
    /// </summary>
    public byte[] DecryptWithEncKey(ReadOnlySpan<byte> ciphertext)
    {
        var iv = BuildTransportIv(_readCounter++);
        return Crypto.AesDecryptGcm(ciphertext, _encKey, iv, ReadOnlySpan<byte>.Empty);
    }

    // ──────────────────────────────────────────────────────────
    //  Internals
    // ──────────────────────────────────────────────────────────

    private void Authenticate(ReadOnlySpan<byte> data)
    {
        if (_transportEstablished) return;
        var combined = new byte[_hash.Length + data.Length];
        _hash.CopyTo(combined, 0);
        data.CopyTo(combined.AsSpan(_hash.Length));
        _hash = Crypto.Sha256(combined);
    }

    private byte[] EncryptTransport(ReadOnlySpan<byte> plaintext)
    {
        var iv = BuildTransportIv(_writeCounter++);
        return Crypto.AesEncryptGcm(plaintext, _encKey, iv, ReadOnlySpan<byte>.Empty);
    }

    private byte[] DecryptTransport(ReadOnlySpan<byte> ciphertext)
    {
        var iv = BuildTransportIv(_readCounter++);
        return Crypto.AesDecryptGcm(ciphertext, _decKey, iv, ReadOnlySpan<byte>.Empty);
    }

    private static byte[] BuildTransportIv(int counter)
    {
        var iv = new byte[IvLength];
        // Use unsigned right-shift (C# 11+: >>>) to avoid sign-extension
        iv[8] = (byte)((uint)counter >> 24);
        iv[9] = (byte)((uint)counter >> 16);
        iv[10] = (byte)((uint)counter >> 8);
        iv[11] = (byte)counter;
        return iv;
    }

    private static byte[] GenerateIv(int counter)
    {
        var iv = new byte[IvLength];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(iv.AsSpan(8), counter);
        return iv;
    }

    private byte[] MixIntoKey(ReadOnlySpan<byte> data)
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
        _writeCounter = 0;
        _readCounter = 0;
        
        // TypeScript Baileys: mixIntoKey does NOT update hash! Only salt and keys!
        
        // Return the expanded key for compatibility
        return expanded;
    }
}
