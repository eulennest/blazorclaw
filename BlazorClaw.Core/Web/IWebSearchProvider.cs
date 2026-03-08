namespace BlazorClaw.Core.Web;

public interface IWebSearchProvider
{
    Task<string> SearchAsync(string query, int count);
    Task<string> FetchAsync(string url);
}

public class WebAggregator(IEnumerable<IWebSearchProvider> providers) : IWebSearchProvider
{
    public async Task<string> SearchAsync(string query, int count)
    {
        var results = new List<string>();
        foreach (var provider in providers)
        {
            try
            {
                results.Add(await provider.SearchAsync(query, count));
            }
            catch
            {
            }
        }
        return string.Join("\n\n---\n\n", results);
    }

    public async Task<string> FetchAsync(string url)
    {
        // Einfache Implementierung: Erster Provider gewinnt
        foreach (var provider in providers)
        {
            try { return await provider.FetchAsync(url); } catch { }
        }
        return "Fetch failed.";
    }
}
