using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools;

public class WeatherParams
{
    [Description("Die Stadt, für die das Wetter abgerufen werden soll")]
    [Required]
    public string City { get; set; } = string.Empty;

    [Description("Einheit für die Temperatur")]
    public TemperatureUnit Unit { get; set; } = TemperatureUnit.Celsius;
}

public enum TemperatureUnit { Celsius, Fahrenheit }

public class WeatherTool : BaseTool<WeatherParams>
{
    public override string Name => "get_weather";
    public override string Description => "Ruft das aktuelle Wetter für eine Stadt ab";

    protected override Task<string> ExecuteInternalAsync(WeatherParams p)
    {
        // Hier würde die API-Logik folgen
        return Task.FromResult($"Das Wetter in {p.City} ist angenehm in {p.Unit}.");
    }
}
