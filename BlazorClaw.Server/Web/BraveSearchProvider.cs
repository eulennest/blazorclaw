using BlazorClaw.Core.Web;
using Microsoft.Extensions.Options;

namespace BlazorClaw.Server.Web;

public class WebSearchOptions
{
    public const string Section = "WebSearch";
    public string BraveApiKey { get; set; } = string.Empty;
}

public class BraveSearchProvider(HttpClient httpClient, IOptions<WebSearchOptions> options) : IWebSearchProvider
{
    private readonly WebSearchOptions _options = options.Value;

    public async Task<string> SearchAsync(string query, int count)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.search.brave.com/res/v1/web/search?q={Uri.EscapeDataString(query)}&count={count}");
        request.Headers.Add("X-Subscription-Token", _options.BraveApiKey);

        using var response = await httpClient.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> FetchAsync(string url)
    {
        throw new NotImplementedException("FetchAsync ist für BraveSearchProvider nicht implementiert, da die API nur Suchanfragen unterstützt.");
    }
}
