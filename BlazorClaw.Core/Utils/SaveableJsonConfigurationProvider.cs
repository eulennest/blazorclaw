using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace BlazorClaw.Core.Utils
{
    public class SaveableJsonConfigurationProvider(SaveableJsonConfigurationSource configurationSource) : JsonConfigurationProvider(configurationSource)
    {
        protected string FileName => Source.Path ?? string.Empty;
        public override void Set(string key, string? value)
        {
            base.Set(key, value);
            SaveToFile(FileName, key, value);
        }

        private static bool SaveToFile(string filepath, string key, object? val)
        {
            string? sJson = File.Exists(filepath)
              ? File.ReadAllText(filepath)
              : null;

            var rootNode = sJson != null
              ? JsonConvert.DeserializeObject<JObject>(sJson)
              : [];

            var cols = key.Split(ConfigurationPath.KeyDelimiter);
            var pathCols = cols.Take(cols.Length - 1).ToArray();

            JObject curObj = rootNode ?? [];
            foreach (var item in pathCols)
            {
                if (!curObj.ContainsKey(item))
                    curObj[item] = new JObject();
                if (curObj[item] is JObject jo) curObj = jo;
            }
            if (val == null)
            {
                curObj.Remove(cols.Last());
            }
            else
            {
                curObj[cols.Last()] = JToken.FromObject(val);
            }


            //save the json to file
            sJson = JsonConvert.SerializeObject(rootNode, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(filepath, sJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
            return true;
        }
    }
}
