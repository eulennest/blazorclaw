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

        private Timer? _saveTimer;
        private readonly object _saveLock = new();
        private const int SaveDelayMs = 100;

        public override void Set(string key, string? value)
        {
            base.Set(key, value);
            if (value == null)
            {
                Data.Select(kvp => kvp.Key).Where(k => k.StartsWith(key + ConfigurationPath.KeyDelimiter)).ToList()
                    .ForEach(k => Data.Remove(k));
            }
            DebouncedSave();
        }

        private void DebouncedSave()
        {
            lock (_saveLock)
            {
                _saveTimer?.Dispose();
                _saveTimer = new Timer(_ => SaveAllToFile(), null, SaveDelayMs, Timeout.Infinite);
            }
        }

        private void SaveAllToFile()
        {
            lock (_saveLock)
            {
                string? sJson = File.Exists(FileName)
                  ? File.ReadAllText(FileName)
                  : null;

                var rootNode = sJson != null
                  ? JsonConvert.DeserializeObject<JObject>(sJson)
                  : [];

                // Rebuild entire config from Data dictionary
                foreach (var kvp in Data)
                {
                    SetValueInJson(rootNode ?? [], kvp.Key, kvp.Value);
                }

                // Save
                sJson = JsonConvert.SerializeObject(rootNode, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(FileName, sJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
                OnReload();
            }
        }

        private static void SetValueInJson(JObject rootNode, string key, string? value)
        {
            var cols = key.Split(ConfigurationPath.KeyDelimiter);
            var pathCols = cols.Take(cols.Length - 1).ToArray();

            JObject curObj = rootNode;
            foreach (var item in pathCols)
            {
                if (!curObj.ContainsKey(item))
                {
                    if (value == null) return; // No need to create path if we're just deleting a value
                    curObj[item] = new JObject();
                }
                if (curObj[item] is JObject jo) curObj = jo;
            }

            if (value == null)
            {
                curObj.Remove(cols.Last());
            }
            else
            {
                curObj[cols.Last()] = GetJToken(value);
            }
        }

        protected static JToken GetJToken(object obj)
        {
            if (obj is string str)
            {
                if ("true".Equals(str, StringComparison.InvariantCultureIgnoreCase))
                    return JToken.FromObject(true);
                if ("false".Equals(str, StringComparison.InvariantCultureIgnoreCase))
                    return JToken.FromObject(false);
                if (str.Length > 0 && str.All(char.IsDigit) && str[0] != '0')
                    return JToken.FromObject(long.Parse(str));
            }
            return JToken.FromObject(obj);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _saveTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
