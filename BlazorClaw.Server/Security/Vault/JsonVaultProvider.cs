using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security.Vault;
using BlazorClaw.Core.Utils;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace BlazorClaw.Server.Security.Vault;

public class JsonVaultProvider : IVaultProvider
{
    private readonly string _masterKey;
    private readonly JsonVaultOptions options;
    private readonly MessageContextAccessor mca;
    private readonly ILogger<JsonVaultProvider> logger;

    public JsonVaultProvider(
        IOptions<JsonVaultOptions> options,
        MessageContextAccessor mca,
        ILogger<JsonVaultProvider> logger)
    {
        this.options = options.Value;
        this.mca = mca;
        this.logger = logger;
        _masterKey = options.Value.MasterPassword
            ?? throw new InvalidOperationException("Vault:MasterPassword not configured!");
    }

    private string GetFilePath()
    {
        return Path.Combine(Core.Utils.PathUtils.GetUserBasePath(mca.Context), "secure", options.FilePath);
    }

    public async IAsyncEnumerable<IVaultKey> GetKeysAsync()
    {
        var data = await ReadAsync();

        if (data != null)
            foreach (var item in data)
            {
                yield return new VaultKey() { Key = item.Key, Title = item.Value.Title };
            }
    }

    public async Task<IVaultEntry?> GetSecretAsync(string key)
    {
        var data = (await ReadAsync()) ?? [];
        return (data != null && data.TryGetValue(key, out var val)) ? val : null;
    }

    public async Task<string> SetSecretAsync(string title, string secret, string? note = null, string? key = null)
    {
        var data = (await ReadAsync()) ?? [];
        if (string.IsNullOrWhiteSpace(key))
            key = Guid.NewGuid().ToString();
        data.TryGetValue(key, out var existing);
        existing ??= new VaultEntry() { Key = key };
        existing.Title = title;
        existing.Secret = secret;
        existing.Notes = note ?? existing.Notes;
        data[key] = existing;
        await SaveAsync(data);
        return key;
    }

    private async Task<Dictionary<string, VaultEntry>?> ReadAsync()
    {
        try
        {
            var filePath = GetFilePath();
            if (!File.Exists(filePath)) return null;
            using var sourceStream = File.OpenRead(filePath);
            using var destStream = new MemoryStream();
            await sourceStream.DecryptAsync(destStream, _masterKey, mca.Context?.UserId ?? string.Empty);
            destStream.Position = 0;
            return await JsonSerializer.DeserializeAsync<Dictionary<string, VaultEntry>>(destStream);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to decrypt vault for user {UserId}", mca.Context?.UserId);
            return null; // oder throw, je nach Anforderung
        }
    }

    private async Task SaveAsync(Dictionary<string, VaultEntry> data)
    {
        var filePath = GetFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var tempPath = filePath + ".tmp";

        var userId = mca.Context?.UserId ?? string.Empty;
        using var tempStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(tempStream, data);
        tempStream.Position = 0;

        using (var destStream = File.Create(tempPath))
        {
            await tempStream.EncryptAsync(destStream, _masterKey, userId);
        }
        File.Move(tempPath, filePath, overwrite: true);
        logger.LogInformation("Vault saved for user {UserId}, {Count} entries", userId, data.Count);
    }
}
