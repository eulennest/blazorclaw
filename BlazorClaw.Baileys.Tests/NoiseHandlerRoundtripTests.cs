using Baileys.Crypto;
using Baileys.Types;
using Baileys.Utils;
using Google.Protobuf;
using Xunit;

namespace BlazorClaw.Baileys.Tests;

public class NoiseHandlerRoundtripTests
{
    [Fact]
    public void EncryptDecrypt_Roundtrip_BetweenTwoHandlers()
    {

        // Arrange: Zwei NoiseHandler mit identischen Keys (für Test)
        var keyPair1 = Curve25519Utils.GenerateKeyPair();
        var keyPair2 = Curve25519Utils.GenerateKeyPair();

        var clientNoise = new NoiseHandler(keyPair1);
        var serverNoise = new NoiseHandler(keyPair2);

        var sharedSecret = Curve25519Utils.CalculateAgreement(keyPair2.Private, keyPair1.Public);

        serverNoise.MixIntoKey(sharedSecret);

        var serverHello = new global::Proto.HandshakeMessage
        {
            ServerHello = new ()
            {
                Ephemeral = ByteString.CopyFrom(keyPair2.Public),
                Static = ByteString.CopyFrom(serverNoise.Encrypt(keyPair1.Public))
            }
        };

        var enc  = clientNoise.ProcessHandshake(serverHello);
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