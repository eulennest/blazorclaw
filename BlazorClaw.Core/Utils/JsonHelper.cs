using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorClaw.Core.Utils
{
    public class JsonHelper
    {
        public static JsonSerializerOptions DefaultOptions
        {
            get
            {
                var jo = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    WriteIndented = true
                };
                jo.Converters.Add(new JsonStringEnumConverter());
                return jo;
            }
        }
    }
}
