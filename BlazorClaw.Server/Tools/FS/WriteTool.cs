using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
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
        var vfs = context.Provider.GetRequiredService<IVfsSystem>();

        var path = VfsPath.Parse(VfsPath.Parse("/~/"), p.Path);
        if (path.IsDirectory)
            throw new FileNotFoundException($"Path ist keine Datei: {path}");

        await vfs.CreateDirectoryRecursiveAsync(path.ParentPath);

        var mi = await vfs.GetMetaInfoAsync(path);
        if (!mi.Exists) throw new FileNotFoundException($"Die Datei '{p.Path}' wurde nicht gefunden.", p.Path);

        var mode = p.Mode ?? WriteMode.Create;
        if (mode == WriteMode.Append)
        {
            using var stream = await mi.OpenAsync(FileMode.Append, FileAccess.Write);
            using var reader = new StreamWriter(stream);
            await reader.WriteAsync(p.Content);
            return $"Inhalt erfolgreich an {path} angehängt.";
        }
        else
        {
            if (mode == WriteMode.Create && mi.Exists)
                throw new InvalidOperationException($"Datei existiert bereits: {p.Path}");

            using var stream = await mi.OpenWriteAsync();
            using var reader = new StreamWriter(stream);
            await reader.WriteAsync(p.Content);

            return $"Datei erfolgreich gespeichert unter: {path}";
        }
    }
}
