namespace BlazorClaw.Core.Security.Vault
{
    public class VaultEntry : VaultKey, IVaultEntry
    {
        public virtual string Notes { get; set; } = string.Empty;
        public virtual string Secret { get; set; } = string.Empty;

        public override string ToString() => $"{Title} ({Key})";
    }
    public class VaultKey : IVaultKey
    {
        public virtual string Key { get; set; } = string.Empty;
        public virtual string Title { get; set; } = string.Empty;

        public override string ToString() => $"{Title} ({Key})";
    }
}