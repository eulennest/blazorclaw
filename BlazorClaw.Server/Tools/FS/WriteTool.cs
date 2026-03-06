using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools.FS;

public class WriteTool : BaseTool<WriteTool.Params>
{
    public override string Name => "fs_write";
    public override string Description => "Schreibt einen neuen Inhalt in eine Datei (überschreibt existierende Dateien).";

    public enum WriteMode { Override, Append, Create }

    public class Params
    {
        [Required, Description("Pfad zur Datei")]
        public string Path { get; set; } = string.Empty;
        
        [Required, Description("Neuer Inhalt")]
        public string Content { get; set; } = string.Empty;

        [Description("Modus: Create (Standard), Append oder Override")]
        public WriteMode? Mode { get; set; } = WriteMode.Create;
    }

    protected override async Task<string> ExecuteInternalAsync(Params parameters, ToolContext context)
    {
        var directory = Path.GetDirectoryName(parameters.Path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var mode = parameters.Mode ?? WriteMode.Create;

        if (mode == WriteMode.Create)
        {
            if (File.Exists(parameters.Path))
                throw new InvalidOperationException($"Datei existiert bereits: {parameters.Path}");
            
            await File.WriteAllTextAsync(parameters.Path, parameters.Content);
            return $"Datei neu erstellt unter: {parameters.Path}";
        }
        else if (mode == WriteMode.Append)
        {
            await File.AppendAllTextAsync(parameters.Path, parameters.Content);
            return $"Inhalt erfolgreich an {parameters.Path} angehängt.";
        }
        else // Override
        {
            await File.WriteAllTextAsync(parameters.Path, parameters.Content);
            return $"Datei erfolgreich überschrieben unter: {parameters.Path}";
        }
    }
}
