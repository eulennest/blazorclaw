using BlazorClaw.Core.Web;
using Microsoft.Extensions.Options;

namespace BlazorClaw.Server.Web;

public class WebSearchOptions
{
    public const string Section = "WebSearch";
    public string BraveApiKey { get; set; } = string.Empty;
}

public class BraveSearchProvider : IWebSearchProvider
{
    private readonly HttpClient _httpClient;
    private readonly WebSearchOptions _options;

    public BraveSearchProvider(HttpClient httpClient, IOptions<WebSearchOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string> SearchAsync(string query, int count)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.search.brave.com/res/v1/web/search?q={Uri.EscapeDataString(query)}&count={count}");
        request.Headers.Add("X-Subscription-Token", _options.BraveApiKey);

        var response = await _httpClient.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> FetchAsync(string url)
    {
        return await _httpClient.GetStringAsync(url);
    }
}
