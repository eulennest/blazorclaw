using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace BlazorClaw.Core.Tools;

/// <summary>
/// Base class for tool parameters with variable substitution support
/// </summary>
public abstract class BaseToolParams
{
    [Description("""
    Variable Mappings für sichere Token/Secrets (nicht im Klartext speichern!).
    Format: {"VAR_NAME": "source:item_name"}
    
    Quellen (Sources):
    - "vault:ItemName" → Wird automatisch via vault_get aus Bitwarden Vault geholt.
      ItemName kann Key oder Titel sein (wie in vault_get).
      Beispiel: "vault:Home_Assistant_Token" oder "vault:HA_TOKEN"
    - "env:VAR_NAME" → Aus Umgebungsvariable
      Beispiel: "env:HA_TOKEN"
    
    Dann in anderen Feldern nutzen: @VAR_NAME
    
    Beispiel für Home Assistant (Token wird automatisch aus Vault geholt):
    {
      "bearerToken": "@HA_TOKEN",
      "variableMappings": {
        "HA_TOKEN": "vault:Home_Assistant_Token"
      }
    }
    
    Mehrere Variablen:
    {
      "body": "{"entity_id": "@ENTITY_ID", "brightness": "@BRIGHTNESS"}",
      "variableMappings": {
        "ENTITY_ID": "env:LIGHT_ENTITY",
        "BRIGHTNESS": "vault:Light_Config_Brightness"
      }
    }
    """)]
    public Dictionary<string, string>? VariableMappings { get; set; }

    public async Task ResolveVarsAsync(MessageContext context)
    {
        if (VariableMappings == null) return;
        var vh = context.Provider.GetService<VariableResolverHelper>();
        if (vh == null) return;
        await vh.ResolveMappingsAsync(VariableMappings, context).ConfigureAwait(false);

        foreach (var prop in GetType().GetProperties())
        {
            if (prop.CanWrite && prop.CanRead && (prop.PropertyType == typeof(string)))
            {
                prop.SetValue(this, ReplaceVars(prop.GetValue(this) as string));
            }
            else if (prop.CanWrite && prop.CanRead && (prop.PropertyType == typeof(Dictionary<string, string>)))
            {
                if (prop.GetValue(this) is not Dictionary<string, string> dict) continue;
                foreach (var item in dict.ToList())
                {
                    dict[item.Key] = ReplaceVars(item.Value) ?? string.Empty;
                }
            }
            else if (prop.CanWrite && prop.CanRead && (prop.PropertyType == typeof(string[])))
            {
                if (prop.GetValue(this) is not string[] list) continue;
                var li = new List<string>();
                foreach (var item in list)
                {

                    li.Add(ReplaceVars(item) ?? string.Empty);
                }
                prop.SetValue(this, li.ToArray());
            }
        }
    }

    public string? ReplaceVars(string? value)
    {
        if (value == null || VariableMappings == null || !value.Contains('@')) return value;
        foreach (var item in VariableMappings.OrderByDescending(o => o.Key.Length))
        {
            value = value.Replace("@" + item.Key, item.Value);
        }
        return value;
    }


}
