namespace BlazorClaw.Core.Memory;

public interface IMemorySearchProvider
{
    Task<string> SearchAsync(string query, int maxResults);
}

public class MemorySearchAggregator : IMemorySearchProvider
{
    private readonly IEnumerable<IMemorySearchProvider> _providers;

    public MemorySearchAggregator(IEnumerable<IMemorySearchProvider> providers)
    {
        _providers = providers;
    }

    public async Task<string> SearchAsync(string query, int maxResults)
    {
        var results = new List<string>();
        foreach (var provider in _providers)
        {
            results.Add(await provider.SearchAsync(query, maxResults));
        }
        return string.Join("\n\n---\n\n", results);
    }
}
