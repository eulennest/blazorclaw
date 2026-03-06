namespace BlazorClaw.Core.Memory;

public interface IMemorySearchProvider
{
    IAsyncEnumerable<string> SearchAsync(string[] queries, int maxResults);
}

public class MemorySearchAggregator(IEnumerable<IMemorySearchProvider> providers) : IMemorySearchProvider
{
    private readonly List<IMemorySearchProvider> _providers = [.. providers];

    public async IAsyncEnumerable<string> SearchAsync(string[] queries, int maxResults)
    {
        foreach (var provider in _providers)
        {
            await foreach (var result in provider.SearchAsync(queries, maxResults))
            {
                yield return result;
            }
        }
    }
}
