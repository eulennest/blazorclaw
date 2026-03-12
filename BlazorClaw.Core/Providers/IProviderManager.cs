namespace BlazorClaw.Core.Providers
{
    public interface IProviderConfiguration
    {
        string Uri { get; }
        string? Token { get; }
    }

    public interface IProviderManager
    {
        IEnumerable<string> GetProviders();
        IProviderConfiguration? GetProviderConfig(string provider);
        IAsyncEnumerable<string> GetModelsAsync(string provider);
        IAsyncEnumerable<string> GetModelsAsync();
        Task<bool> SetProviderAsync(string provider, IProviderConfiguration config);
    }
    public class ProviderConfiguration : IProviderConfiguration
    {
        public required string Uri { get; set; }
        public string? Token { get; set; }
    }
}
