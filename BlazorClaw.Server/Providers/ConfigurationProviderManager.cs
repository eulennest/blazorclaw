using BlazorClaw.Core.Providers;
using Microsoft.Extensions.Configuration;

namespace BlazorClaw.Server.Providers
{
    public class ConfigurationProviderManager : IProviderManager
    {
        private readonly IConfiguration _configuration;
        private readonly List<IProviderConfiguration> _providers = new();

        public ConfigurationProviderManager(IConfiguration configuration)
        {
            _configuration = configuration;
            LoadFromConfig();
        }

        private void LoadFromConfig()
        {
            var providerSections = _configuration.GetSection("Providers").GetChildren();
            foreach (var section in providerSections)
            {
                _providers.Add(new ProviderConfiguration
                {
                    Name = section.Key,
                    Uri = section["Uri"] ?? string.Empty,
                    Token = section["Token"] ?? string.Empty,
                    Models = section.GetSection("Models").Get<List<string>>() ?? new List<string>()
                });
            }
        }

        public void RegisterProvider(IProviderConfiguration provider)
        {
            _providers.Add(provider);
        }

        IEnumerable<IProviderConfiguration> IProviderManager.GetProviders()
        {
            return _providers;
        }
    }
    
    public class ProviderConfiguration : IProviderConfiguration
    {
        public string Name { get; set; } = string.Empty;
        public string Uri { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public List<string> Models { get; set; } = new();
    }
}
