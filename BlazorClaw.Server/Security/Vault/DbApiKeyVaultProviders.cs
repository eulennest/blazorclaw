using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.Models;
using BlazorClaw.Core.Security.Vault;
using Microsoft.EntityFrameworkCore;

namespace BlazorClaw.Server.Security.Vault;

public class DbUserApiKeyVaultProvider(ApplicationDbContext db, MessageContextAccessor mca) : IVaultProvider
{
    public async IAsyncEnumerable<IVaultKey> GetKeysAsync()
    {
        var userId = GetRequiredUserId();
        var items = await db.ApiKeys
            .Where(o => o.UserId == userId && o.OAuthTokenId == null)
            .OrderBy(o => o.Identifier)
            .ToListAsync();

        foreach (var item in items)
            yield return ToEntry(item);
    }

    public async Task<IVaultEntry?> GetSecretAsync(string key)
    {
        var userId = GetRequiredUserId();
        var item = await db.ApiKeys
            .FirstOrDefaultAsync(o => o.UserId == userId && o.OAuthTokenId == null && o.Id.ToString() == key);
        return item == null ? null : ToEntry(item);
    }

    public async Task<string> SetSecretAsync(string title, string secret, string? note = null, string? key = null)
    {
        var userId = GetRequiredUserId();
        ApiKey? item = null;
        if (!string.IsNullOrWhiteSpace(key))
        {
            item = await db.ApiKeys.FirstOrDefaultAsync(o => o.Id.ToString() == key && o.UserId == userId && o.OAuthTokenId == null)
                ?? throw new KeyNotFoundException($"API-Key '{key}' nicht gefunden oder nicht schreibbar.");
        }
        else
        {
            item = await db.ApiKeys.FirstOrDefaultAsync(o => o.Identifier == title && o.UserId == userId && o.OAuthTokenId == null);
        }

        if (item == null)
        {
            item = new ApiKey
            {
                Identifier = title,
                UserId = userId,
                Value = secret,
                TokenType = note,
                UpdatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            db.ApiKeys.Add(item);
        }
        else
        {
            item.Identifier = title;
            item.Value = secret;
            item.TokenType = note;
            item.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return item.Id.ToString();
    }

    private string GetRequiredUserId()
    {
        var userId = mca.Context?.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("DB-Key-Vault benötigt einen User-Kontext.");
        return userId;
    }

    private static VaultEntry ToEntry(ApiKey item) => new()
    {
        Key = item.Id.ToString(),
        Title = item.Identifier,
        Secret = item.Value ?? string.Empty,
        Notes = item.TokenType ?? string.Empty
    };
}

public class DbReadonlyApiKeyVaultProvider(ApplicationDbContext db, MessageContextAccessor mca) : IVaultProvider
{
    public async IAsyncEnumerable<IVaultKey> GetKeysAsync()
    {
        var userId = mca.Context?.UserId;
        var items = await db.ApiKeys
            .Include(o => o.OAuthToken)
                .ThenInclude(o => o!.Server)
            .Where(o => o.UserId == null || (userId != null && o.UserId == userId && o.OAuthTokenId != null))
            .OrderBy(o => o.Identifier)
            .ToListAsync();

        foreach (var item in items)
            yield return ToEntry(item);
    }

    public async Task<IVaultEntry?> GetSecretAsync(string key)
    {
        var userId = mca.Context?.UserId;
        var item = await db.ApiKeys
            .Include(o => o.OAuthToken)
                .ThenInclude(o => o!.Server)
            .FirstOrDefaultAsync(o => o.Id.ToString() == key && (o.UserId == null || (userId != null && o.UserId == userId && o.OAuthTokenId != null)));
        return item == null ? null : ToEntry(item);
    }

    public Task<string> SetSecretAsync(string title, string secret, string? note = null, string? key = null)
        => throw new InvalidOperationException("Readonly DB-Key-Vault unterstützt kein Schreiben.");

    private static VaultEntry ToEntry(ApiKey item)
    {
        var kind = item.OAuthTokenId != null ? "oauth" : "system";
        var noteParts = new List<string> { $"Kind: {kind}" };
        if (!string.IsNullOrWhiteSpace(item.TokenType)) noteParts.Add($"TokenType: {item.TokenType}");
        if (!string.IsNullOrWhiteSpace(item.UserId)) noteParts.Add($"UserId: {item.UserId}");
        if (item.OAuthToken?.Server != null) noteParts.Add($"OAuthServer: {item.OAuthToken.Server.Name}");
        if (item.OAuthToken?.ExpiresAt != default) noteParts.Add($"ExpiresAt: {item.OAuthToken.ExpiresAt:O}");

        return new VaultEntry
        {
            Key = item.Id.ToString(),
            Title = item.Identifier,
            Secret = item.OAuthTokenId != null ? item.OAuthToken?.AccessToken ?? string.Empty : item.Value ?? string.Empty,
            Notes = string.Join(Environment.NewLine, noteParts)
        };
    }
}
