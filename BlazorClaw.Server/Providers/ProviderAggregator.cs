using System.Collections.Generic;
using BlazorClaw.Core.Providers;

namespace BlazorClaw.Server.Providers
{
    public class ProviderAggregator : IProviderManager
    {
        private readonly IEnumerable<IProviderManager> _managers;

        public ProviderAggregator(IEnumerable<IProviderManager> managers)
        {
            _managers = managers;
        }

        public void RegisterProvider(IProviderConfiguration provider)
        {
            throw new NotSupportedException("Aggregator does not support direct registration.");
        }

        public IEnumerable<IProviderConfiguration> GetProviders()
        {
            return _managers.SelectMany(m => m.GetProviders());
        }
    }
}
