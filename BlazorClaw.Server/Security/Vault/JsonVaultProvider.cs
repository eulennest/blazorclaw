using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security.Vault;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace BlazorClaw.Server.Security.Vault;

public class JsonVaultProvider(
    IOptions<JsonVaultOptions> options,
    MessageContextAccessor mca,
    IVfsSystem vfs,
    ILogger<JsonVaultProvider> logger) : IVaultProvider
{
    private readonly string _masterKey = options.Value.MasterPassword
            ?? throw new InvalidOperationException("Vault:MasterPassword not configured!");
    private readonly JsonVaultOptions options = options.Value;

    private VfsFile GetFilePath()
    {
        return new VfsFile(vfs, VfsPath.Parse(VfsPath.Parse("/~secure/"), options.FilePath, VfsPathParseMode.File));
    }

    public async IAsyncEnumerable<IVaultKey> GetKeysAsync(string? searchQuery = null)
    {
        var data = await ReadAsync();
        var query = searchQuery?.Trim();

        if (data != null)
            foreach (var item in data)
            {
                if (!string.IsNullOrWhiteSpace(query)
                    && !item.Value.Title.Contains(query, StringComparison.InvariantCultureIgnoreCase)
                    && !item.Value.Notes.Contains(query, StringComparison.InvariantCultureIgnoreCase))
                    continue;

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

    public async Task RemoveSecretAsync(string key)
    {
        var data = (await ReadAsync()) ?? [];
        if (!data.Remove(key))
            throw new KeyNotFoundException($"Vault-Eintrag '{key}' nicht gefunden.");
        await SaveAsync(data);
    }

    private async Task<Dictionary<string, VaultEntry>?> ReadAsync()
    {
        try
        {
            var filePath = GetFilePath();
            if (!await filePath.VFS.ExistsAsync(filePath.Path)) return null;
            using var sourceStream = await filePath.OpenAsync(FileMode.Open, FileAccess.Read);
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
        if (!await filePath.VFS.ExistsAsync(filePath.Path))
            await filePath.VFS.CreateDirectoryRecursiveAsync(filePath.Path.ParentPath);
        var userId = mca.Context?.UserId ?? string.Empty;
        using var tempStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(tempStream, data);
        tempStream.Position = 0;
        using (var destStream = await filePath.OpenAsync(FileMode.Create, FileAccess.Write))
        {
            await tempStream.EncryptAsync(destStream, _masterKey, userId);
        }
        logger.LogInformation("Vault saved for user {UserId}, {Count} entries", userId, data.Count);
    }
}
