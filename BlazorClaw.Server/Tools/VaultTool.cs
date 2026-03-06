using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Security.Vault;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools;


public class VaultGetParams
{
    [Description("")]
    [Required]
    public string Key { get; set; } = string.Empty;
}

public class VaultSetParams : VaultGetParams
{
    [Description("")]
    [Required]
    public string Secret { get; set; } = string.Empty;
}


public class VaultGetTool : BaseTool<VaultGetParams>
{
    public override string Name => "vault_get";
    public override string Description => "";

    protected override Task<string> ExecuteInternalAsync(VaultGetParams p, ToolContext context)
    {
        var vp = context.ServiceProvider.GetRequiredService<IVaultProvider>();
        return vp.GetSecretAsync(p.Key);
    }
}

public class VaultSetTool : BaseTool<VaultSetParams>
{
    public override string Name => "vault_set";
    public override string Description => "";

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
    public override string Description => "";

    protected override async Task<string> ExecuteInternalAsync(EmptyParams p, ToolContext context)
    {
        var vp = context.ServiceProvider.GetRequiredService<IVaultProvider>();
        var list = await vp.GetKeysAsync().ToListAsync();
        return string.Join(Environment.NewLine, list);
    }
}
