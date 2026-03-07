using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlazorClaw.Core.Utils
{
    public class JsonHelper
    {
        public static JsonSerializerOptions DefaultOptions
        {
            get
            {
                var jo = new JsonSerializerOptions
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
