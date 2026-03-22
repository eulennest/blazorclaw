namespace BlazorClaw.Server.Security.Vault
{
    public class JsonVaultOptions
    {
        public const string Section = "Vault";
        public string FilePath { get; set; } = "./vault.enc.json";
        public string? MasterPassword { get; set; }
    }
}