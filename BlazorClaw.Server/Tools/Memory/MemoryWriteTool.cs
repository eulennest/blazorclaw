using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Memory;

public class MemoryWriteTool : BaseTool<MemoryWriteTool.Params>
{
    public override string Name => "memory_write";
    public override string Description => "Schreibt Markdown-Inhalt in eine Memory-Datei (relative zum /memory Ordner).";

    public enum WriteMode { Create, Append, Override }

    public class Params
    {
        [Required, Description("Dateiname (immer relativ, Name + .md)")]
        public string FileName { get; set; } = string.Empty;

        [Required, Description("Markdown-Inhalt der geschrieben werden soll")]
        public string Content { get; set; } = string.Empty;

        [Description("Modus: Create (Standard, gibt Fehler wenn schon existiert), Append oder Override")]
        public WriteMode? Mode { get; set; } = WriteMode.Create;
    }

    protected override async Task<string> ExecuteInternalAsync(Params parameters, MessageContext context)
    {
        var fullPath = context.GetMemoryPath(parameters.FileName);
        var safeFileName = Path.GetFileName(fullPath);
        var _memoryPath = Path.GetDirectoryName(fullPath);
        if (_memoryPath != null && !Directory.Exists(_memoryPath))
            Directory.CreateDirectory(_memoryPath);

        var mode = parameters.Mode ?? WriteMode.Create;

        if (mode == WriteMode.Create)
        {
            if (File.Exists(fullPath))
                throw new InvalidOperationException($"Memory-Datei existiert bereits: {safeFileName}");

            await File.WriteAllTextAsync(fullPath, parameters.Content);
            return $"Memory-Datei '{safeFileName}' neu erstellt.";
        }
        else if (mode == WriteMode.Append)
        {
            await File.AppendAllTextAsync(fullPath, parameters.Content);
            return $"Inhalt erfolgreich an '{safeFileName}' angehängt.";
        }
        else // Override
        {
            await File.WriteAllTextAsync(fullPath, parameters.Content);
            return $"Memory-Datei '{safeFileName}' erfolgreich überschrieben.";
        }
    }
}
