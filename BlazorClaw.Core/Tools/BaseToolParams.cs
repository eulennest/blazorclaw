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
    - "vault:ItemName" → Aus Bitwarden Vault (z.B. "vault:Home_Assistant_Token")
    - "env:VAR_NAME" → Aus Umgebungsvariable (z.B. "env:HA_TOKEN")
    
    Dann in anderen Feldern nutzen: @VAR_NAME
    
    Beispiel für Home Assistant:
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
}
