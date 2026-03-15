using BlazorClaw.Core.Commands;

namespace BlazorClaw.Core.Memory;

public interface IMemorySearchProvider
{
    IAsyncEnumerable<string> SearchAsync(string[] queries, int maxResults, MessageContext? context);
}

public class MemorySearchAggregator(IEnumerable<IMemorySearchProvider> providers) : IMemorySearchProvider
{
    private readonly List<IMemorySearchProvider> _providers = [.. providers];

    public async IAsyncEnumerable<string> SearchAsync(string[] queries, int maxResults, MessageContext? context)
    {
        foreach (var provider in _providers)
        {
            await foreach (var result in provider.SearchAsync(queries, maxResults, context))
            {
                yield return result;
            }
        }
    }
}
