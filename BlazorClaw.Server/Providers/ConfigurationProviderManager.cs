using BlazorClaw.Core.Providers;

namespace BlazorClaw.Server.Providers
{
    public class ConfigurationProviderManager : IProviderManager
    {
        private readonly IConfiguration _configuration;
        private readonly List<ProviderConfiguration> _providers;

        public ConfigurationProviderManager(IConfiguration configuration)
        {
            _configuration = configuration;
            _providers = LoadFromConfig();
        }

        public IAsyncEnumerable<string> GetModelsAsync(string provider)
        {
            return _providers
                .FirstOrDefault(p => p.Name.Equals(provider, StringComparison.OrdinalIgnoreCase))?
                .Models.ToAsyncEnumerable() ?? AsyncEnumerable.Empty<string>();
        }

        public IProviderConfiguration? GetProviderConfig(string provider)
        {
            return _providers.FirstOrDefault(p => p.Name.Equals(provider, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<string> GetProviders()
        {
            return _providers.Select(p => p.Name);
        }

        private List<ProviderConfiguration> LoadFromConfig()
        {
            var list = new List<ProviderConfiguration>();
            var providerSections = _configuration.GetSection("Providers").GetChildren();
            foreach (var section in providerSections)
            {
                list.Add(new ProviderConfiguration
                {
                    Name = section.Key,
                    Uri = section["Uri"] ?? string.Empty,
                    Token = section["Token"] ?? string.Empty,
                    Models = section.GetSection("Models").Get<List<string>>() ?? []
                });
            }
            return list;
        }
    }

    public class ProviderConfiguration : IProviderConfiguration
    {
        public string Name { get; set; } = string.Empty;
        public string Uri { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public List<string> Models { get; set; } = [];
    }
}
