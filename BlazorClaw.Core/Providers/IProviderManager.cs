namespace BlazorClaw.Core.Providers
{
    public interface IProviderConfiguration
    {
        string Name { get; }
        string Uri { get; }
        string Token { get; }
        List<string> Models { get; }
    }

    public interface IProviderManager
    {
        void RegisterProvider(IProviderConfiguration provider);
        IEnumerable<IProviderConfiguration> GetProviders();
    }
}
