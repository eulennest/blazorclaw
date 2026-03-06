using System.Collections.Generic;
using BlazorClaw.Core.Providers;

namespace BlazorClaw.Core.Providers
{
    public interface IProviderAggregator
    {
        void RegisterProvider(IProviderConfiguration provider);
        IEnumerable<IProviderConfiguration> GetProviders();
    }

    public class ProviderAggregator : IProviderAggregator
    {
        private readonly List<IProviderConfiguration> _providers = new();

        public void RegisterProvider(IProviderConfiguration provider)
        {
            _providers.Add(provider);
        }

        public IEnumerable<IProviderConfiguration> GetProviders()
        {
            return _providers;
        }
    }
}
