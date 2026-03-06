namespace BlazorClaw.Server.Providers
{
    using BlazorClaw.Core.Providers;
    using Microsoft.Extensions.Configuration;

    public class ConfigurationProviderAggregator : IProviderManager
    {
        private readonly IConfiguration _configuration;

        public ConfigurationProviderAggregator(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IEnumerable<string> GetProviderNames()
        {
            return _configuration.GetSection("Providers").GetChildren().Select(c => c.Key);
        }

        public IProviderConfiguration? GetProvider(string providerName)
        {
            var section = _configuration.GetSection($"Providers:{providerName}");
            if (!section.Exists()) return null;

            return new ProviderConfiguration
            {
                Name = providerName,
                Uri = section["Uri"] ?? string.Empty,
                Token = section["Token"] ?? string.Empty,
                Models = section.GetSection("Models").Get<List<string>>() ?? new List<string>()
            };
        }

        private class ProviderConfiguration : IProviderConfiguration
        {
            public string Name { get; set; } = string.Empty;
            public string Uri { get; set; } = string.Empty;
            public string Token { get; set; } = string.Empty;
            public List<string> Models { get; set; } = new();
        }
    }
}