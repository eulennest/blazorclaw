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
    }

    protected override async Task<string> ExecuteInternalAsync(Params parameters, ToolContext context)
    {
        if (!File.Exists(parameters.Path))
            throw new FileNotFoundException($"Datei nicht gefunden: {parameters.Path}");

        var content = await File.ReadAllTextAsync(parameters.Path);
        if (!content.Contains(parameters.OldText))
            return "Fehler: Alter Text nicht gefunden.";

        var newContent = content.Replace(parameters.OldText, parameters.NewText);
        await File.WriteAllTextAsync(parameters.Path, newContent);
        
        return "Datei erfolgreich editiert.";
    }
}
