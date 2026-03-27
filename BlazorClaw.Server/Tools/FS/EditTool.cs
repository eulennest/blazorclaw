using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.FS;

public class EditTool : BaseTool<EditTool.Params>
{
    public override string Name => "fs_edit";
    public override string Description => "Editiert eine Datei durch Ersetzen von Text (Suchen & Ersetzen).";

    public class Params : IWorkingPaths
    {
        [Required, Description("Pfad zur Datei")]
        public string Path { get; set; } = string.Empty;

        [Required, Description("Text, der ersetzt werden soll")]
        public string OldText { get; set; } = string.Empty;

        [Required, Description("Neuer Text")]
        public string NewText { get; set; } = string.Empty;

        [Description("Wenn true, werden alle Vorkommnisse ersetzt, sonst nur das erste (Standard: false)")]
        public bool? Multiple { get; set; } = false;

        public IEnumerable<string> GetPaths()
        {
            yield return Path;
        }
    }

    protected override async Task<string> ExecuteInternalAsync(Params parameters, MessageContext context)
    {
        var vfs = context.Provider.GetRequiredService<IVfsSystem>();

        var path = VfsPath.Parse(VfsPath.Parse("/~/"), parameters.Path);
        if (path.IsDirectory)
            throw new FileNotFoundException($"Path ist keine Datei: {parameters.Path}");

        var mi = await vfs.GetMetaInfoAsync(path);
        if (!mi.Exists)
            throw new FileNotFoundException($"Datei nicht gefunden: {parameters.Path}");

        string newContent = string.Empty;
        using (var strm = await mi.OpenReadAsync())
        {
            using var st = new StreamReader(strm);
            var content = await st.ReadToEndAsync();

            if (!content.Contains(parameters.OldText))
                return "Fehler: Alter Text nicht gefunden.";

            if (parameters.Multiple == true)
            {
                newContent = content.Replace(parameters.OldText, parameters.NewText);
            }
            else
            {
                var index = content.IndexOf(parameters.OldText);
                newContent = content.Remove(index, parameters.OldText.Length).Insert(index, parameters.NewText);
            }
        }
        using (var strm = await mi.OpenWriteAsync())
        {
            using var st = new StreamWriter(strm);
            await st.WriteAsync(newContent);
        }
        return $"Datei '{path}' erfolgreich editiert.";
    }
}
