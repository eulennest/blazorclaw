using BlazorClaw.Core.Security.Vault;
using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace BlazorClaw.Server.Tools;


public class VaultGetParams
{
    [Description("Der Schlüssel des Geheimnisses")]
    [Required]
    public string Key { get; set; } = string.Empty;
}

public class VaultSetParams
{
    [Required]
    [Description("Der Title des Geheimnis")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Description("Das Geheimnis selbst")]
    public string Secret { get; set; } = string.Empty;

    [Description("Zusätzliche Notiz")]
    public string? Note { get; set; }

    [Description("Der Schlüssel des Geheimnisses  nur bei Update")]
    public string? Key { get; set; }
}

public class VaultGetTool : BaseTool<VaultGetParams>
{
    public override string Name => "vault_get";
    public override string Description => "Holt ein Geheimnis aus dem Vault";

    protected override async Task<string> ExecuteInternalAsync(VaultGetParams p, ToolContext context)
    {
        var vp = context.ServiceProvider.GetRequiredService<IVaultProvider>();

        var secret = await vp.GetSecretAsync(p.Key) ?? throw new KeyNotFoundException($"Kein Geheimnis mit Schlüssel '{p.Key}' gefunden.");
        var sb = new StringBuilder();
        sb.AppendLine($"Key: {secret.Key}");
        sb.AppendLine($"Title: {secret.Title}");
        sb.AppendLine($"Secret: {secret.Secret}");
        if (!string.IsNullOrWhiteSpace(secret.Notes))
            sb.AppendLine($"Notes: {secret.Notes}");
        return sb.ToString();
    }
}

public class VaultSetTool : BaseTool<VaultSetParams>
{
    public override string Name => "vault_set";
    public override string Description => "Setzt ein Geheimnis im Vault (Return: Key des Geheimnisses)";

    protected override async Task<string> ExecuteInternalAsync(VaultSetParams p, ToolContext context)
    {
        var vp = context.ServiceProvider.GetRequiredService<IVaultProvider>();
        return await vp.SetSecretAsync(p.Title, p.Secret,p.Note, p.Key);
    }
}


public class VaultListTool : BaseTool<EmptyParams>
{
    public override string Name => "vault_list";
    public override string Description => "Listet alle Schlüssel im Vault auf";

    protected override async Task<string> ExecuteInternalAsync(EmptyParams p, ToolContext context)
    {
        var vp = context.ServiceProvider.GetRequiredService<IVaultProvider>();
        var list = await vp.GetKeysAsync().ToListAsync();
        return string.Join(Environment.NewLine, list.Select(o => $"{o.Key}: {o.Title}"));
    }
}
