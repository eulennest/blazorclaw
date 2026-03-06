using System.Text.Json;
using BlazorClaw.Core.Web;

namespace BlazorClaw.Server.Web;

public class BraveSearchOptions
{
    public string ApiKey { get; set; } = string.Empty;
}

public class BraveSearchProvider : IWebSearchProvider
{
    private readonly HttpClient _httpClient;
    private readonly BraveSearchOptions _options;

    public BraveSearchProvider(HttpClient httpClient, BraveSearchOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<string> SearchAsync(string query, int count)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.search.brave.com/res/v1/web/search?q={Uri.EscapeDataString(query)}&count={count}");
        request.Headers.Add("X-Subscription-Token", _options.ApiKey);

        var response = await _httpClient.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> FetchAsync(string url)
    {
        // Einfaches Fetching (kann später mit Readability-Logic erweitert werden)
        return await _httpClient.GetStringAsync(url);
    }
}
