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
                    curObj[item] = new JObject();
                if (curObj[item] is JObject jo) curObj = jo;
            }

            if (value == null)
            {
                curObj.Remove(cols.Last());
            }
            else
            {
                curObj[cols.Last()] = JToken.FromObject(value);
            }
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
