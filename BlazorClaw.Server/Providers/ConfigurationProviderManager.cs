using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Providers;
using BlazorClaw.Core.Utils;
using Microsoft.Extensions.AI;
using OpenAI;

namespace BlazorClaw.Server.Providers
{
    public class ConfigurationProviderManager : IProviderManager
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient httpClient;
        private List<ConfigProviderConfiguration> _providers;

        public ConfigurationProviderManager(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            this.httpClient = httpClient;
            _providers = LoadFromConfig();
        }

        public ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct = default)
        {
            if (!ModelPath.TryDecompose(model, out var head, out var tail))
                throw new ArgumentException("Invalid model path.", nameof(model));
            var conf = GetProviderConfig(head) ?? throw new InvalidOperationException($"Provider '{head}' not found.");

            var opts = new OpenAIClientOptions()
            {
                Endpoint = new Uri(conf.Uri)
            };

            var chat = new OpenAI.Chat.ChatClient(tail, new System.ClientModel.ApiKeyCredential(conf.Token ?? string.Empty), opts).AsIChatClient();
            return new ValueTask<IChatClient>(chat);
        }

        public ValueTask<IEmbeddingGenerator<string, Embedding<float>>> GetEmbeddingClientAsync(string model, CancellationToken ct = default)
        {
            throw new NotImplementedException();
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
                    httpClient.InitProvider(prov);
                    var ret = await httpClient.GetFromJsonAsync<ModelListResponse>("models");

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

        public IEnumerable<ProviderInfo> GetProviders()
        {
            return _providers.Select(p => new ProviderInfo(p.Name, FeatureSupport.Chat));
        }

        public Task<bool> SetProviderAsync(string provider, IProviderConfiguration config)
        {
            _configuration[$"Providers:{provider}:Uri"] = config.Uri;
            _configuration[$"Providers:{provider}:Token"] = config.Token;
            _providers = LoadFromConfig();
            return Task.FromResult(true);
        }

        private List<ConfigProviderConfiguration> LoadFromConfig()
        {
            var list = new List<ConfigProviderConfiguration>();
            var providerSections = _configuration.GetSection("Providers").GetChildren();
            foreach (var section in providerSections)
            {
                list.Add(new ConfigProviderConfiguration
                {
                    Name = section.Key,
                    Uri = section["Uri"] ?? string.Empty,
                    Token = section["Token"],
                    Models = section.GetSection("Models").Get<List<string>>() ?? []
                });
            }
            return list;
        }
    }

    public class ConfigProviderConfiguration : ProviderConfiguration
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Models { get; set; } = [];
    }
}
