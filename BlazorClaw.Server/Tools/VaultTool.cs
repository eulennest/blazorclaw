using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security.Vault;
using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace BlazorClaw.Server.Tools;

public class VaultGetParams
{
    [Description("Der Schlüssel des Geheimnisses oder der genaue Title")]
    [Required]
    public string Key { get; set; } = string.Empty;

    [Description("Optionaler Vault-Provider, z.B. json-main. Wenn leer, werden alle Provider durchsucht")]
    public string? Provider { get; set; }
}

public class VaultSetParams
{
    [Required]
    [Description("Vault-Provider, in dem gespeichert werden soll")]
    public string Provider { get; set; } = string.Empty;

    [Required]
    [Description("Der Title des Geheimnis")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Description("Das Geheimnis selbst")]
    public string Secret { get; set; } = string.Empty;

    [Description("Zusätzliche Notiz")]
    public string? Note { get; set; }

    [Description("Der Schlüssel des Geheimnisses nur bei Update")]
    public string? Key { get; set; }
}

public class VaultListParams
{
    [Description("Optionaler Vault-Provider. Wenn leer, werden alle Provider angezeigt")]
    public string? Provider { get; set; }
}

public class VaultRemoveParams
{
    [Required]
    [Description("Vault-Provider, aus dem gelöscht werden soll")]
    public string Provider { get; set; } = string.Empty;

    [Required]
    [Description("Der Schlüssel des Geheimnisses, das gelöscht werden soll")]
    public string Key { get; set; } = string.Empty;
}

public class VaultGetTool : BaseTool<VaultGetParams>
{
    public override string Name => "vault_get";
    public override string Description => "Holt ein Geheimnis aus dem Vault. Optional kann ein Provider angegeben werden.";

    protected override async Task<string> ExecuteInternalAsync(VaultGetParams p, MessageContext context)
    {
        var vm = context.Provider.GetRequiredService<IVaultManager>();
        var secret = await vm.GetSecretAsync(p.Key, p.Provider);
        if (secret == null) throw new KeyNotFoundException($"Kein Geheimnis mit Schlüssel '{p.Key}' gefunden.");

        var sb = new StringBuilder();
        sb.AppendLine($"Provider: {secret.Provider}");
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
    public override string Description => "Setzt ein Geheimnis in einem bestimmten Vault-Provider (Return: Key des Geheimnisses)";

    protected override async Task<string> ExecuteInternalAsync(VaultSetParams p, MessageContext context)
    {
        var vm = context.Provider.GetRequiredService<IVaultManager>();
        return await vm.SetSecretAsync(p.Provider, p.Title, p.Secret, p.Note, p.Key);
    }
}

public class VaultListTool : BaseTool<VaultListParams>
{
    public override string Name => "vault_list";
    public override string Description => "Listet alle Schlüssel in einem Vault oder providerübergreifend auf";

    protected override async Task<string> ExecuteInternalAsync(VaultListParams p, MessageContext context)
    {
        var vm = context.Provider.GetRequiredService<IVaultManager>();
        var list = await vm.GetKeysAsync(p.Provider).ToListAsync();
        return string.Join(Environment.NewLine, list.Select(o => $"{o.Provider}: {o.Key}: {o.Title}"));
    }
}

public class VaultRemoveTool : BaseTool<VaultRemoveParams>
{
    public override string Name => "vault_rm";
    public override string Description => "Löscht ein Geheimnis aus einem bestimmten schreibbaren Vault-Provider";

    protected override async Task<string> ExecuteInternalAsync(VaultRemoveParams p, MessageContext context)
    {
        var vm = context.Provider.GetRequiredService<IVaultManager>();
        await vm.RemoveSecretAsync(p.Provider, p.Key);
        return $"Removed: {p.Provider}: {p.Key}";
    }
}

public class VaultProviderListTool : BaseTool<EmptyParams>
{
    public override string Name => "vault_provider_list";
    public override string Description => "Listet alle verfügbaren Vault-Provider auf";

    protected override Task<string> ExecuteInternalAsync(EmptyParams p, MessageContext context)
    {
        var vm = context.Provider.GetRequiredService<IVaultManager>();
        var lines = vm.GetProviders().Select(o => $"{o.Id} ({o.Type}){(o.CanWrite ? string.Empty : " [read-only]")}: {o.Title}{(string.IsNullOrWhiteSpace(o.Description) ? string.Empty : $" - {o.Description}")}");
        return Task.FromResult(string.Join(Environment.NewLine, lines));
    }
}
