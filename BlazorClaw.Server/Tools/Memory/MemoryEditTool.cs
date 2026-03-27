using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Memory;

public class MemoryEditTool : BaseTool<MemoryEditTool.Params>
{
    public override string Name => "memory_edit";
    public override string Description => "Editiert eine Memory-Datei (Markdown-Format, relative zum /~memory/ Ordner).";

    public class Params
    {
        [Required, Description("Dateiname (immer relativ,  Name + .md)")]
        public string FileName { get; set; } = string.Empty;

        [Required, Description("Text, der ersetzt werden soll")]
        public string OldText { get; set; } = string.Empty;

        [Required, Description("Neuer Text")]
        public string NewText { get; set; } = string.Empty;

        [Description("Wenn true, werden alle Vorkommnisse ersetzt (Standard: false)")]
        public bool? Multiple { get; set; } = false;
    }

    protected override async Task<string> ExecuteInternalAsync(Params parameters, MessageContext context)
    {
        var fullPath = context.GetMemoryPath(parameters.FileName);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Memory-Datei nicht gefunden: {parameters.FileName}");

        var content = await File.ReadAllTextAsync(fullPath);
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

        await File.WriteAllTextAsync(fullPath, newContent);
        return $"Memory-Datei '{parameters.FileName}' erfolgreich editiert.";
    }
}
