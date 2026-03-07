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
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true,
                    IgnoreReadOnlyFields = true,
                    IgnoreReadOnlyProperties = true,
                    WriteIndented = true
                };
                jo.Converters.Add(new JsonStringEnumConverter());
                return jo;
            }
        }
    }
}
