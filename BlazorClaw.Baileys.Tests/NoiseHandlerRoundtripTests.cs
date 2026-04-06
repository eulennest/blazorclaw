using Baileys.Types;
using Baileys.Utils;
using Xunit;

namespace BlazorClaw.Baileys.Tests;

public class NoiseHandlerRoundtripTests
{
    [Fact]
    public void EncryptDecrypt_Roundtrip_BetweenTwoHandlers()
    {
        // Arrange: Zwei NoiseHandler mit identischen Keys (für Test)
        var keyPair = new KeyPair(new byte[32], new byte[32]);
        
        var clientNoise = new NoiseHandler(keyPair);
        var serverNoise = new NoiseHandler(keyPair);

        // Manuell Keys setzen (simuliert Handshake)
        var testKey = new byte[32];
        clientNoise.SetEncKey(testKey);
        clientNoise.SetDecKey(testKey);
        clientNoise.SetTransportEstablished(true);
        
        serverNoise.SetEncKey(testKey);
        serverNoise.SetDecKey(testKey);
        serverNoise.SetTransportEstablished(true);

        // Act: Nachricht verschlüsseln (Client → Server)
        var plaintext = new byte[] { 0x01, 0x02, 0x03 };
        var ciphertext = clientNoise.Encrypt(plaintext);
        
        // Nachricht entschlüsseln (Server)
        var decrypted = serverNoise.Decrypt(ciphertext);

        // Assert: Plaintext == Decrypted
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void DecryptWithEncKey_Roundtrip()
    {
        // Arrange: Zwei NoiseHandler mit identischen Keys
        var keyPair = new KeyPair(new byte[32], new byte[32]);
        var noiseHandler = new NoiseHandler(keyPair);
        
        // Manuell Keys setzen (simuliert Handshake)
        var testKey = new byte[32];
        noiseHandler.SetEncKey(testKey);
        noiseHandler.SetDecKey(testKey);
        noiseHandler.SetTransportEstablished(true);

        // Act: Nachricht verschlüsseln/entschlüsseln (mit EncKey)
        var plaintext = new byte[] { 0x04, 0x05, 0x06 };
        noiseHandler.SetCounter(0); // Counter zurücksetzen
        var ciphertext = noiseHandler.Encrypt(plaintext);
        noiseHandler.SetCounter(0); // Counter zurücksetzen
        var decrypted = noiseHandler.DecryptWithEncKey(ciphertext);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }
}