using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Security.Vault;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools;


public class VaultGetParams
{
    [Description("Der Schlüssel des Geheimnisses")]
    [Required]
    public string Key { get; set; } = string.Empty;
}

public class VaultSetParams : VaultGetParams
{
    [Description("Das Geheimnis selbst")]
    [Required]
    public string Secret { get; set; } = string.Empty;
}


public class VaultGetTool : BaseTool<VaultGetParams>
{
    public override string Name => "vault_get";
    public override string Description => "Holt ein Geheimnis aus dem Vault";

    protected override async Task<string> ExecuteInternalAsync(VaultGetParams p, ToolContext context)
    {
        var vp = context.ServiceProvider.GetRequiredService<IVaultProvider>();
        return await vp.GetSecretAsync(p.Key) ?? string.Empty;
    }
}

public class VaultSetTool : BaseTool<VaultSetParams>
{
    public override string Name => "vault_set";
    public override string Description => "Setzt ein Geheimnis im Vault";

    protected override async Task<string> ExecuteInternalAsync(VaultSetParams p, ToolContext context)
    {
        var vp = context.ServiceProvider.GetRequiredService<IVaultProvider>();
        await vp.SetSecretAsync(p.Key, p.Secret);
        return "Secret saved.";
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
        return string.Join(Environment.NewLine, list);
    }
}
