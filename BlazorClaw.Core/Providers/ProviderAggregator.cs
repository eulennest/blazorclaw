namespace BlazorClaw.Core.Providers
{

    public class ProviderAggregator(IEnumerable<IProviderManager> providers) : IProviderManager
    {
        private readonly List<IProviderManager> _providers = [.. providers];

        public IAsyncEnumerable<string> GetModelsAsync(string provider)
        {
            provider = SplitProviderFromModel(provider);
            var prov = _providers.FirstOrDefault(o => o.GetProviders().Contains(provider));
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


        public IProviderConfiguration? GetProviderConfig(string provider)
        {
            provider = SplitProviderFromModel(provider);
            var prov = _providers.FirstOrDefault(o => o.GetProviders().Contains(provider));
            return prov?.GetProviderConfig(provider);
        }

        public IEnumerable<string> GetProviders()
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
            var prov = _providers.FirstOrDefault(o => o.GetProviders().Contains(provider));
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
    }
}
