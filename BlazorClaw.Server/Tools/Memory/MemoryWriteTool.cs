using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Server.Tools.Memory;

public class MemoryWriteTool : BaseTool<MemoryWriteTool.Params>
{
    private readonly string _memoryPath = "./memory";

    public override string Name => "memory_write";
    public override string Description => "Schreibt Inhalt in eine Memory-Datei (relative zum /memory Ordner).";

    public enum WriteMode { Create, Append, Override }

    public class Params
    {
        [Required, Description("Dateiname (ohne Pfad, nur Name + .md)")]
        public string FileName { get; set; } = string.Empty;
        
        [Required, Description("Inhalt der geschrieben werden soll")]
        public string Content { get; set; } = string.Empty;

        [Description("Modus: Create (Standard), Append oder Override")]
        public WriteMode? Mode { get; set; } = WriteMode.Create;
    }

    protected override async Task<string> ExecuteInternalAsync(Params parameters, ToolContext context)
    {
        var safeFileName = Path.GetFileName(parameters.FileName);
        if (!safeFileName.EndsWith(".md")) safeFileName += ".md";
        var fullPath = Path.Combine(_memoryPath, safeFileName);

        if (!Directory.Exists(_memoryPath))
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
