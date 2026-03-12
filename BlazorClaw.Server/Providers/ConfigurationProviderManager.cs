using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Providers;

namespace BlazorClaw.Server.Providers
{
    public class ConfigurationProviderManager : IProviderManager
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient httpClient;
        private readonly List<ProviderConfiguration> _providers;

        public ConfigurationProviderManager(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            this.httpClient = httpClient;
            _providers = LoadFromConfig();
        }


        public async IAsyncEnumerable<string> GetModelsAsync()
        {
            foreach (var item in _providers)
            {
                await foreach (var model in GetModelsAsync(item.Name))
                    yield return $"{item.Name}/{model}";
            }
        }

        public async IAsyncEnumerable<string> GetModelsAsync(string provider)
        {
            var prov = _providers.FirstOrDefault(p => p.Name.Equals(provider, StringComparison.OrdinalIgnoreCase));
            if (prov != null && (prov.Models?.Count ?? 0) == 0)
            {
                prov.Models ??= [];
                try
                {
                    var uri = new Uri(new Uri(prov.Uri), "models");
                    var ret = await httpClient.GetFromJsonAsync<ModelListResponse>(uri);

                    if (ret?.Data != null)
                    {
                        foreach (var model in ret.Data)
                        {
                            if (!string.IsNullOrWhiteSpace(model.Id))
                                prov.Models.Add(model.Id);
                        }
                    }
                }
                catch (Exception)
                {
                }

            }

            if (prov?.Models != null)
                foreach (var item in prov.Models)
                {
                    yield return item;
                }
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
