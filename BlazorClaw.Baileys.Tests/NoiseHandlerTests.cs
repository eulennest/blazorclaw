using Baileys.Types;
using Baileys.Utils;
using Xunit;

namespace BlazorClaw.Baileys.Tests;

public class NoiseHandlerTests
{
    [Fact]
    public void MixIntoKey_UpdatesSaltAndKey()
    {
        // Arrange
        var keyPair = new KeyPair(new byte[32], new byte[32]);
        var noiseHandler = new NoiseHandler(keyPair);
        var oldSalt = noiseHandler.GetSalt();
        var oldEncKey = noiseHandler.GetEncKey();
        var sharedSecret = new byte[32];

        // Act
        noiseHandler.MixIntoKey(sharedSecret);

        // Assert
        Assert.NotEqual(oldSalt, noiseHandler.GetSalt());
        Assert.NotEqual(oldEncKey, noiseHandler.GetEncKey());
        Assert.Equal(noiseHandler.GetEncKey(), noiseHandler.GetDecKey()); // decKey = encKey
    }

    [Fact]
    public void EncryptDecrypt_Roundtrip()
    {
        // Arrange
        var keyPair = new KeyPair(new byte[32], new byte[32]);
        var noiseHandler = new NoiseHandler(keyPair);
        var plaintext = new byte[] { 0x01, 0x02, 0x03 };

        // Manuell Keys und Counter setzen (simuliert Transport-Modus)
        var testKey = new byte[32];
        Array.Fill(testKey, (byte)0x42); // Test-Key
        noiseHandler.SetEncKey(testKey);
        noiseHandler.SetDecKey(testKey);
        noiseHandler.SetCounter(1); // Counter für Encrypt/Decrypt synchronisieren

        // Act
        var ciphertext = noiseHandler.Encrypt(plaintext);
        noiseHandler.SetCounter(1); // Counter für Decrypt zurücksetzen
        var decrypted = noiseHandler.Decrypt(ciphertext);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact(Skip = "Requires valid handshake mock data (integration test)")]
    public void ProcessHandshake_GeneratesClientFinish()
    {
        // Arrange
        var keyPair = new KeyPair(new byte[32], new byte[32]);
        var noiseHandler = new NoiseHandler(keyPair);
        // Manuelle Mock-Daten (wie in Rust/TypeScript: 32 Byte Ephemeral, 48 Byte Static/Payload)
        var staticCiphertext = new byte[48];
        Array.Fill(staticCiphertext, (byte)0xAA); // Test-Daten (wie in Rust)
        var payloadCiphertext = new byte[48];
        Array.Fill(payloadCiphertext, (byte)0xBB); // Test-Daten (wie in Rust)

        var serverHello = new Proto.HandshakeMessage
        {
            ServerHello = new Proto.HandshakeMessage.Types.ServerHello
            {
                Ephemeral = Google.Protobuf.ByteString.CopyFrom(new byte[32]),
                Static = Google.Protobuf.ByteString.CopyFrom(staticCiphertext),
                Payload = Google.Protobuf.ByteString.CopyFrom(payloadCiphertext)
            }
        };

        // Act
        var clientFinish = noiseHandler.ProcessHandshake(serverHello);

        // Assert
        Assert.NotNull(clientFinish);
        Assert.NotEmpty(clientFinish);
    }

    [Fact]
    public void Authenticate_UpdatesHash()
    {
        // Arrange
        var keyPair = new KeyPair(new byte[32], new byte[32]);
        var noiseHandler = new NoiseHandler(keyPair);
        var oldHash = noiseHandler.GetHash();
        var data = new byte[] { 0x01, 0x02, 0x03 };

        // Act
        noiseHandler.MixHash(data); // MixHash ruft Authenticate auf

        // Assert
        Assert.NotEqual(oldHash, noiseHandler.GetHash());
    }
}