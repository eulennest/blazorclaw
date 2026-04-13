using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Apigen.Vaultwarden.Client;
using Apigen.Vaultwarden.Models;
using BlazorClaw.Core.Security.Vault;
using Microsoft.Extensions.Options;

namespace BlazorClaw.Server.Security.Vault;

public class BitwardenOptions
{
    public const string Section = "Vault:Bitwarden";
    public string ApiUrl { get; set; } = string.Empty;
    public string IdentityUrl { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MasterPassword { get; set; } = string.Empty;
}

public class BitwardenVaultProvider(
    IOptions<BitwardenOptions> options,
    IServiceProvider services,
    ILogger<BitwardenVaultProvider> logger) : IVaultProvider
{
    private readonly BitwardenOptions _options = options.Value;
    private readonly IServiceProvider _services = services;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private BitwardenSession? _session;

    public async IAsyncEnumerable<IVaultKey> GetKeysAsync()
    {
        var session = await GetSessionAsync();
        if (session.UserSymmetricKey.Length == 0) yield break;
        var sync = await GetSyncAsync(session);

        foreach (var cipher in sync.Ciphers?.Where(o => o.DeletedDate == null) ?? [])
        {
            var key = GetCipherId(cipher);
            if (key == null) continue;

            yield return new VaultKey
            {
                Key = key,
                Title = TryDecryptCipherName(cipher, session)
            };
        }
    }

    public async Task<IVaultEntry?> GetSecretAsync(string key)
    {
        var session = await GetSessionAsync();
        if (session.UserSymmetricKey.Length == 0) return null;

        var sync = await GetSyncAsync(session);
        var cipher = sync.Ciphers?.FirstOrDefault(o => string.Equals(o.Id?.ToString(), key, StringComparison.InvariantCultureIgnoreCase));
        if (cipher == null) return null;

        return ToEntry(cipher, session);
    }

    public Task<string> SetSecretAsync(string title, string secret, string? note = null, string? key = null)
        => throw new NotSupportedException("Bitwarden/Vaultwarden Schreiben ist über den Apigen-Provider noch nicht implementiert.");

    public async Task RemoveSecretAsync(string key)
    {
        var session = await GetSessionAsync();
        if (session.UserSymmetricKey.Length == 0) return;

        await session.Client.Ciphers.DeleteAsync(key);
        session.Sync = null;
    }

    private async Task<BitwardenSession> GetSessionAsync()
    {
        if (_session != null) return _session;

        await _initLock.WaitAsync();
        try
        {
            if (_session != null) return _session;

            var cfg = await ResolveLoginConfigAsync();
            _session = await CreateSessionAsync(cfg);
            logger.LogInformation("Bitwarden/Vaultwarden session initialized against {BaseUrl} for {Email}", cfg.BaseUrl, cfg.Email);
            return _session;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fehler bei der Initialisierung der Bitwarden/Vaultwarden Session: {Message}", ex.Message);
            _session = new(null!, [], []);
            return _session;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static async Task<SyncResponseModel> GetSyncAsync(BitwardenSession session)
    {
        if (session.Sync != null) return session.Sync;
        session.Sync = await session.Client.Sync.SyncGetAsync(new SyncGetRequest { ExcludeDomains = true });
        return session.Sync;
    }

    private async Task<BitwardenLoginConfig> ResolveLoginConfigAsync()
    {
        if (HasDirectLoginConfig(_options))
        {
            return new BitwardenLoginConfig(
                NormalizeBaseUrl(_options.ApiUrl, _options.IdentityUrl),
                _options.Email,
                _options.MasterPassword);
        }

        var vm = _services.GetRequiredService<IVaultManager>();

        foreach (var provider in vm.GetProviders().Where(o => o.Id.StartsWith("db-", StringComparison.InvariantCultureIgnoreCase)))
        {
            await foreach (var key in vm.GetKeysAsync(provider.Id))
            {
                if (!key.Title.Contains("vaultwarden", StringComparison.InvariantCultureIgnoreCase))
                    continue;
                logger.LogInformation("Found Secret {key}", key);

                var entry = await vm.GetSecretAsync(key.Key, provider.Id);
                if (entry == null || string.IsNullOrWhiteSpace(entry.Secret))
                    continue;
                logger.LogInformation("Load Secret {entry}", entry);

                var url = TryParseUrl(entry.Notes);
                if (!string.IsNullOrWhiteSpace(url))
                    logger.LogInformation("-Found URL {url}", url);
                var email = TryParseNamedValue(entry.Notes, "email", "username", "user", "login");
                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(email))
                    continue;
                logger.LogInformation("-Found EMAIL {email}", email);

                logger.LogInformation("Bitwarden provider bootstrapped from {ProviderId} using key {Title}", provider.Id, entry.Title);
                return new BitwardenLoginConfig(url, email, entry.Secret);
            }
        }

        throw new InvalidOperationException("Bitwarden/Vaultwarden Konfiguration fehlt. Erwartet wird entweder Vault:Bitwarden mit ApiUrl/Email/MasterPassword oder ein db-* Key mit 'vaultwarden' im Titel, URL und Email in den Notes und dem Master-Passwort im Secret.");
    }

    private async Task<BitwardenSession> CreateSessionAsync(BitwardenLoginConfig cfg)
    {
        using var preloginHttpClient = new HttpClient { BaseAddress = new Uri(EnsureTrailingSlash(cfg.BaseUrl)) };
        var preloginClient = new VaultwardenApiClient(preloginHttpClient);
        var prelogin = await preloginClient.Accounts.AccountsPostPreloginAsync(new PasswordPreloginRequestModel { Email = cfg.Email });

        var stretchedKey = DeriveStretchedKey(cfg.Email, cfg.MasterPassword, prelogin);
        var hashedPassword = HashMasterPassword(cfg.Email, cfg.MasterPassword, prelogin);

        using var authHttpClient = new HttpClient { BaseAddress = new Uri(EnsureTrailingSlash(cfg.BaseUrl)) };
        var authClient = new VaultwardenApiClient(authHttpClient);
        var tokenResponse = await authClient.Connect.ConnectTokenAsync(new ConnectTokenRequest
        {
            GrantType = "password",
            Username = cfg.Email,
            Password = hashedPassword,
            Scope = "api offline_access",
            ClientId = "cli",
            DeviceType = 9,
            DeviceIdentifier = Guid.NewGuid().ToString(),
            DeviceName = "blazorclaw"
        });

        if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            throw new InvalidOperationException("Vaultwarden Login lieferte kein AccessToken.");
        if (string.IsNullOrWhiteSpace(tokenResponse.Key))
            throw new InvalidOperationException("Vaultwarden Login lieferte keinen User-Key.");

        var client = VaultwardenApiClient.WithBearer(tokenResponse.AccessToken, cfg.BaseUrl);
        var userSymmetricKey = DecryptEncString(tokenResponse.Key, stretchedKey);
        var sync = await client.Sync.SyncGetAsync(new SyncGetRequest { ExcludeDomains = true });
        var orgKeys = DecryptOrganizationKeys(sync, tokenResponse.PrivateKey, userSymmetricKey);

        return new BitwardenSession(client, userSymmetricKey, orgKeys)
        {
            Sync = sync
        };
    }

    private static Dictionary<Guid, byte[]> DecryptOrganizationKeys(SyncResponseModel? sync, string? privateKey, byte[] userSymmetricKey)
    {
        var result = new Dictionary<Guid, byte[]>();
        if (sync?.Profile?.Organizations == null || string.IsNullOrWhiteSpace(privateKey))
            return result;

        byte[] rsaPrivateKeyDer = DecryptEncString(privateKey, userSymmetricKey);
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(rsaPrivateKeyDer, out _);

        foreach (var org in sync.Profile.Organizations)
        {
            if (org.Id == null || string.IsNullOrWhiteSpace(org.Key))
                continue;

            try
            {
                result[org.Id.Value] = DecryptRsa(org.Key, rsa);
            }
            catch
            {
                // einzelne Org-Keys ignorieren, persönlicher Vault soll trotzdem funktionieren
            }
        }

        return result;
    }

    private static VaultEntry ToEntry(CipherDetailsResponseModel cipher, BitwardenSession session)
    {
        var key = GetCipherId(cipher) ?? throw new InvalidOperationException("Cipher ohne Id kann nicht als VaultEntry abgebildet werden.");
        var itemKey = ResolveItemKey(cipher, session);
        var notes = TryDecrypt(cipher.Notes, itemKey);
        var secret = cipher.Type switch
        {
            CipherType.Login => TryDecrypt(cipher.Login?.Password, itemKey),
            CipherType.SecureNote => notes,
            CipherType.SshKey => TryDecrypt(cipher.SshKey?.PrivateKey, itemKey),
            _ => string.Empty
        };

        var extra = new List<string>();
        var username = TryDecrypt(cipher.Login?.Username, itemKey);
        if (!string.IsNullOrWhiteSpace(username)) extra.Add($"Username: {username}");
        var firstUri = cipher.Login?.Uris?.FirstOrDefault()?.Uri ?? cipher.Login?.Uri;
        var uri = TryDecrypt(firstUri, itemKey);
        if (!string.IsNullOrWhiteSpace(uri)) extra.Add($"Uri: {uri}");
        if (!string.IsNullOrWhiteSpace(notes)) extra.Add(notes);

        return new VaultEntry
        {
            Key = key,
            Title = TryDecryptCipherName(cipher, session),
            Secret = secret,
            Notes = string.Join(Environment.NewLine, extra.Where(o => !string.IsNullOrWhiteSpace(o)))
        };
    }

    private static string TryDecryptCipherName(CipherDetailsResponseModel cipher, BitwardenSession session)
        => TryDecrypt(cipher.Name, ResolveItemKey(cipher, session));

    private static byte[] ResolveItemKey(CipherDetailsResponseModel cipher, BitwardenSession session)
    {
        var baseKey = session.UserSymmetricKey;
        if (cipher.OrganizationId != null && session.OrganizationKeys.TryGetValue(cipher.OrganizationId.Value, out var orgKey))
            baseKey = orgKey;

        if (!string.IsNullOrWhiteSpace(cipher.Key))
        {
            try { return DecryptEncString(cipher.Key, baseKey); }
            catch { }
        }

        return baseKey;
    }

    private static string? GetCipherId(CipherDetailsResponseModel cipher) => cipher.Id?.ToString();

    private static string TryDecrypt(string? encString, byte[] key)
    {
        if (string.IsNullOrWhiteSpace(encString)) return string.Empty;
        try
        {
            return Encoding.UTF8.GetString(DecryptEncString(encString, key));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static byte[] DeriveStretchedKey(string email, string masterPassword, PasswordPreloginResponseModel prelogin)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(masterPassword);
        var saltBytes = Encoding.UTF8.GetBytes(email.ToLowerInvariant());
        var iterations = prelogin.KdfIterations ?? 600000;

        if ((prelogin.Kdf ?? KdfType.Pbkdf2Sha256) != KdfType.Pbkdf2Sha256)
            throw new NotSupportedException("Aktuell wird nur PBKDF2-SHA256 für Vaultwarden Login unterstützt.");

        byte[] masterKey = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, saltBytes, iterations, HashAlgorithmName.SHA256, 32);
        byte[] encKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, 32, info: Encoding.UTF8.GetBytes("enc"));
        byte[] macKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, 32, info: Encoding.UTF8.GetBytes("mac"));
        return [.. encKey, .. macKey];
    }

    private static string HashMasterPassword(string email, string masterPassword, PasswordPreloginResponseModel prelogin)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(masterPassword);
        var saltBytes = Encoding.UTF8.GetBytes(email.ToLowerInvariant());
        var iterations = prelogin.KdfIterations ?? 600000;

        if ((prelogin.Kdf ?? KdfType.Pbkdf2Sha256) != KdfType.Pbkdf2Sha256)
            throw new NotSupportedException("Aktuell wird nur PBKDF2-SHA256 für Vaultwarden Login unterstützt.");

        byte[] masterKey = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, saltBytes, iterations, HashAlgorithmName.SHA256, 32);
        byte[] masterPasswordHash = Rfc2898DeriveBytes.Pbkdf2(masterKey, passwordBytes, 1, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(masterPasswordHash);
    }

    private static byte[] DecryptRsa(string encString, RSA rsa)
    {
        int dotIdx = encString.IndexOf('.');
        int encType = int.Parse(encString[..dotIdx]);
        byte[] ct = Convert.FromBase64String(encString[(dotIdx + 1)..].Split('|')[0]);
        var padding = encType == 3 ? RSAEncryptionPadding.OaepSHA256 : RSAEncryptionPadding.OaepSHA1;
        return rsa.Decrypt(ct, padding);
    }

    private static byte[] DecryptEncString(string encString, byte[] key)
    {
        int dotIdx = encString.IndexOf('.');
        int encType = int.Parse(encString[..dotIdx]);
        string[] parts = encString[(dotIdx + 1)..].Split('|');

        byte[] iv = Convert.FromBase64String(parts[0]);
        byte[] ct = Convert.FromBase64String(parts[1]);
        byte[] ek = key[..32];
        byte[] mk = key[32..64];

        if (encType == 2 && parts.Length >= 3)
        {
            byte[] mac = Convert.FromBase64String(parts[2]);
            byte[] macData = [.. iv, .. ct];
            byte[] computedMac = HMACSHA256.HashData(mk, macData);
            if (!CryptographicOperations.FixedTimeEquals(computedMac, mac))
                throw new CryptographicException("MAC verification failed");
        }

        using var aes = Aes.Create();
        aes.Key = ek;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        return aes.CreateDecryptor().TransformFinalBlock(ct, 0, ct.Length);
    }

    private static bool HasDirectLoginConfig(BitwardenOptions options)
        => !string.IsNullOrWhiteSpace(options.Email)
            && !string.IsNullOrWhiteSpace(options.MasterPassword)
            && (!string.IsNullOrWhiteSpace(options.ApiUrl) || !string.IsNullOrWhiteSpace(options.IdentityUrl));

    private static string NormalizeBaseUrl(string? apiUrl, string? identityUrl)
        => !string.IsNullOrWhiteSpace(apiUrl) ? apiUrl : identityUrl ?? string.Empty;

    private static string EnsureTrailingSlash(string value) => value.EndsWith('/') ? value : value + "/";

    private static string? TryParseUrl(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;
        var match = Regex.Match(notes, @"https?://[^\s]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Value.TrimEnd('/', ';', ',', '.') : null;
    }

    private static string? TryParseNamedValue(string? notes, params string[] names)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;

        foreach (var line in notes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = line.IndexOf(':');
            if (idx <= 0) continue;
            var key = line[..idx].Trim();
            if (!names.Any(o => key.Equals(o, StringComparison.InvariantCultureIgnoreCase)))
                continue;
            var value = line[(idx + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return null;
    }

    private sealed record BitwardenLoginConfig(string BaseUrl, string Email, string MasterPassword);

    private sealed class BitwardenSession(VaultwardenApiClient client, byte[] userSymmetricKey, Dictionary<Guid, byte[]> organizationKeys)
    {
        public VaultwardenApiClient Client { get; } = client;
        public byte[] UserSymmetricKey { get; } = userSymmetricKey;
        public Dictionary<Guid, byte[]> OrganizationKeys { get; } = organizationKeys;
        public SyncResponseModel? Sync { get; set; }
    }
}
