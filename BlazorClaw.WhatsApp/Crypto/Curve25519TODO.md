# Curve25519 Implementation TODO

**Status:** Placeholder — needs proper implementation

## Current Situation
- libsignal-protocol-dotnet has Curve25519 API but source is not available
- Pure C# implementations are complex and error-prone
- External libraries (libsodium.net, Chaos.NaCl) not available in standard NuGet

## Options

### Option 1: P/Invoke to libsodium-core (Recommended)
```bash
dotnet add package libsodium-core
```
Then use:
```csharp
using Sodium;
var keyPair = PublicKeyBox.GenerateKeyPair();
var sharedSecret = ScalarMult.Compute(privateKey, publicKey);
```

### Option 2: Use DLL from libsodium (Windows/Linux/macOS)
- Download libsodium binary for platform
- P/Invoke to crypto_scalarmult_curve25519

### Option 3: Port from another language
- Reference: https://github.com/jedisct1/libsodium/blob/master/src/libsodium/crypto_scalarmult/curve25519/ref10/
- Full Montgomery ladder + field arithmetic

### Option 4: Wait for libsignal stable API
- libsignal-protocol-dotnet is still young (v2.8.1)
- Official .NET Signal bindings may become available

## Impact
- SignalProtocol.cs currently throws NotImplementedException for ECDH
- CryptoUtils.GenerateCurve25519Keypair() → not implemented
- CryptoUtils.Curve25519SharedSecret() → not implemented
- WhatsAppClient cannot complete key exchange yet

## Next Steps
1. Install libsodium-core or equivalent
2. Implement wrappers in Curve25519.cs
3. Update SignalProtocol to use real ECDH
4. Test key exchange flow

**Date:** 2026-04-05
**Blocker:** No suitable .NET NuGet for Curve25519 in standard repos
