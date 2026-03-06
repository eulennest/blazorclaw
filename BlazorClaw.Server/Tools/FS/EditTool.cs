using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools.FS;

public class EditTool : BaseTool<EditTool.Params>
{
    public override string Name => "fs_edit";
    public override string Description => "Editiert eine Datei durch Ersetzen von Text (Suchen & Ersetzen).";

    public class Params
    {
        [Required, Description("Pfad zur Datei")]
        public string Path { get; set; } = string.Empty;
        
        [Required, Description("Text, der ersetzt werden soll")]
        public string OldText { get; set; } = string.Empty;
        
        [Required, Description("Neuer Text")]
        public string NewText { get; set; } = string.Empty;

        [Description("Wenn true, werden alle Vorkommnisse ersetzt, sonst nur das erste (Standard: false)")]
        public bool? Multiple { get; set; } = false;
    }

    protected override async Task<string> ExecuteInternalAsync(Params parameters, ToolContext context)
    {
        if (!File.Exists(parameters.Path))
            throw new FileNotFoundException($"Datei nicht gefunden: {parameters.Path}");

        var content = await File.ReadAllTextAsync(parameters.Path);
        if (!content.Contains(parameters.OldText))
            return "Fehler: Alter Text nicht gefunden.";

        string newContent;
        if (parameters.Multiple == true)
        {
            newContent = content.Replace(parameters.OldText, parameters.NewText);
        }
        else
        {
            var index = content.IndexOf(parameters.OldText);
            newContent = content.Remove(index, parameters.OldText.Length).Insert(index, parameters.NewText);
        }
        
        await File.WriteAllTextAsync(parameters.Path, newContent);
        
        return "Datei erfolgreich editiert.";
    }
}
