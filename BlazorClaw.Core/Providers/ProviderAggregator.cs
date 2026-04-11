using Microsoft.Extensions.AI;

namespace BlazorClaw.Core.Providers
{

    public class ProviderAggregator(IEnumerable<IProviderManager> providers) : IProviderManager
    {
        private readonly List<IProviderManager> _providers = [.. providers];

        public IAsyncEnumerable<string> GetModelsAsync(string provider)
        {
            provider = SplitProviderFromModel(provider);
            var prov = FindProvider(provider);
            return prov?.GetModelsAsync(provider) ?? AsyncEnumerable.Empty<string>();
        }
        public async IAsyncEnumerable<string> GetModelsAsync()
        {
            foreach (var item in _providers)
            {
                await foreach (var model in item.GetModelsAsync())
                    yield return model;
            }
        }

        public IEnumerable<ProviderInfo> GetProviders()
        {
            return _providers.SelectMany(o => o.GetProviders());
        }
        public static string SplitProviderFromModel(string model)
        {
            var parts = model.Split('/', 2);
            return parts?.FirstOrDefault() ?? "default";
        }

        public async Task<bool> SetProviderAsync(string provider, IProviderConfiguration config)
        {
            var prov = FindProvider(provider);
            if (prov != null) return await prov.SetProviderAsync(provider, config);

            foreach (var item in _providers)
            {
                try
                {
                    if (await item.SetProviderAsync(provider, config))
                        return true;
                }
                catch (Exception)
                { }
            }
            return false;
        }

        private IProviderManager? FindProvider(string provider)
        {
            return _providers.FirstOrDefault(o => o.GetProviders().Any(p => p.Id == provider));
        }

        public ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct = default)
        {
            var provider = SplitProviderFromModel(model);
            var prov = FindProvider(provider);
            return prov?.GetChatClientAsync(model, ct) ?? throw new Exception($"Provider '{provider}' not found.");
        }

        public ValueTask<IEmbeddingGenerator<string, Embedding<float>>> GetEmbeddingClientAsync(string model, CancellationToken ct = default)
        {
            var provider = SplitProviderFromModel(model);
            var prov = FindProvider(provider);
            return prov?.GetEmbeddingClientAsync(provider, ct) ?? throw new Exception($"Provider '{provider}' not found.");
        }
    }
}
