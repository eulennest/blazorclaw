namespace BlazorClaw.Core.Providers
{
    public interface IProviderConfiguration
    {
        string Uri { get; }
        string Token { get; }
    }

    public interface IProviderManager
    {
        IEnumerable<string> GetProviders();
        IProviderConfiguration? GetProviderConfig(string provider);
        IAsyncEnumerable<string> GetModelsAsync(string provider);
    }
}
