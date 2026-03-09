using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Web;

public class WeatherTool(IHttpClientFactory httpClientFactory) : BaseTool<WeatherTool.Params>
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();

    public override string Name => "get_weather";
    public override string Description => "Ruft das aktuelle Wetter für eine Stadt ab (via wttr.in).";

    public class Params
    {
        [Required, Description("Die Stadt, für die das Wetter abgerufen werden soll")]
        public string City { get; set; } = string.Empty;
    }

    protected override async Task<string> ExecuteInternalAsync(Params parameters, MessageContext context)
    {
        try
        {
            // wttr.in bietet eine einfache Text-API
            var response = await _httpClient.GetStringAsync($"https://wttr.in/{Uri.EscapeDataString(parameters.City)}?format=3");
            return response;
        }
        catch (Exception ex)
        {
            return $"Wetterabruf fehlgeschlagen: {ex.Message}";
        }
    }
}
