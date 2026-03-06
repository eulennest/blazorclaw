namespace BlazorClaw.Core.Web;

public interface IWebSearchProvider
{
    Task<string> SearchAsync(string query, int count);
    Task<string> FetchAsync(string url);
}

public class WebAggregator : IWebSearchProvider
{
    private readonly IEnumerable<IWebSearchProvider> _providers;

    public WebAggregator(IEnumerable<IWebSearchProvider> providers)
    {
        _providers = providers;
    }

    public async Task<string> SearchAsync(string query, int count)
    {
        var results = new List<string>();
        foreach (var provider in _providers)
        {
            results.Add(await provider.SearchAsync(query, count));
        }
        return string.Join("\n\n---\n\n", results);
    }

    public async Task<string> FetchAsync(string url)
    {
        // Einfache Implementierung: Erster Provider gewinnt
        foreach (var provider in _providers)
        {
            try { return await provider.FetchAsync(url); } catch { continue; }
        }
        return "Fetch failed.";
    }
}
