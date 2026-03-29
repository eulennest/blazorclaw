using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using ReverseMarkdown.Converters;
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
        var vfs = context.Provider.GetRequiredService<IVfsSystem>();
        if (parameters.FileName.StartsWith('/')) parameters.FileName = parameters.FileName[1..];
        var path = VfsPath.Parse(PathUtils.VfsMemory, parameters.FileName, VfsPathParseMode.File);
        if (path.IsDirectory)
            throw new FileNotFoundException($"Path ist keine Datei: {parameters.FileName}");
        if (!PathUtils.VfsMemory.IsParentOf(path)) throw new InvalidPathException(parameters.FileName);

        var mi = await vfs.GetMetaInfoAsync(path);
        if (!mi.Exists)
            throw new FileNotFoundException($"Memory-Datei nicht gefunden: {parameters.FileName}");

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
        return $"Memory-Datei '{parameters.FileName}' erfolgreich editiert.";
    }
}
