using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.FS;

public class WriteTool : BaseTool<WriteTool.Params>
{
    public override string Name => "fs_write";
    public override string Description => "Schreibt einen neuen Inhalt in eine Datei (überschreibt existierende Dateien).";

    public enum WriteMode { Override, Append, Create }

    public class Params : IWorkingPaths
    {
        [Required, Description("Pfad zur Datei")]
        public string Path { get; set; } = string.Empty;

        [Required, Description("Neuer Inhalt")]
        public string Content { get; set; } = string.Empty;

        [Description("Modus: Create (Standard), Append oder Override")]
        public WriteMode? Mode { get; set; } = WriteMode.Create;
        public IEnumerable<string> GetPaths()
        {
            yield return Path;
        }
    }

    protected override async Task<string> ExecuteInternalAsync(Params p, MessageContext context)
    {
        var path = Path.Combine(context.GetWorkspacePath(), p.Path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var mode = p.Mode ?? WriteMode.Create;

        if (mode == WriteMode.Create)
        {
            if (File.Exists(path))
                throw new InvalidOperationException($"Datei existiert bereits: {p.Path}");

            await File.WriteAllTextAsync(path, p.Content);
            return $"Datei neu erstellt unter: {p.Path}";
        }
        else if (mode == WriteMode.Append)
        {
            await File.AppendAllTextAsync(path, p.Content);
            return $"Inhalt erfolgreich an {p.Path} angehängt.";
        }
        else // Override
        {
            await File.WriteAllTextAsync(path, p.Content);
            return $"Datei erfolgreich überschrieben unter: {p.Path}";
        }
    }
}
