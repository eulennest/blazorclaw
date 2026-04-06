using System.Collections;
using System.Text;

namespace Baileys.Types;

/// <summary>
/// The binary-node structure WhatsApp uses internally for protocol communication.
/// </summary>
public sealed class BinaryNode
{
    /// <summary>XML-style tag, e.g. "message", "iq", etc.</summary>
    public required string Tag { get; init; }

    /// <summary>Key-value attributes on the node.</summary>
    public Dictionary<string, string> Attrs { get; init; } = new();

    /// <summary>
    /// Content is either a list of child nodes, a raw string, or raw bytes.
    /// Null means the node has no content.
    /// </summary>
    public BinaryNodeContent? Content { get; init; }


    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append($"<{Tag}");
        sb.Append(string.Concat(Attrs.Select(kv => $" {kv.Key}=\"{kv.Value}\"")));
        sb.Append(Content == null ? " />" : ">");
        sb.Append(Content?.ToString());
        sb.Append(Content == null ? "" : $"</{Tag}>");
        return sb.ToString().Trim();
    }

}

/// <summary>Discriminated union for <see cref="BinaryNode.Content"/>.</summary>
public abstract class BinaryNodeContent { }

/// <summary>Node content that is a list of child <see cref="BinaryNode"/> nodes.</summary>
public sealed class BinaryNodeList(IReadOnlyList<BinaryNode> children) : BinaryNodeContent, IReadOnlyList<BinaryNode>
{
    public BinaryNode this[int index] => Children[index];

    public IReadOnlyList<BinaryNode> Children { get; } = children;

    public int Count => Children.Count;

    public IEnumerator<BinaryNode> GetEnumerator()
    {
        return Children.GetEnumerator();
    }

    public override string ToString()
    {
        return string.Concat(Children.Select(c => c.ToString()));
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)Children).GetEnumerator();
    }
}

/// <summary>Node content that is a plain UTF-8 string.</summary>
public sealed class BinaryNodeString(string value) : BinaryNodeContent
{
    public string Value { get; } = value;

    public override string ToString()
    {
        return Value;
    }
}

/// <summary>Node content that is raw binary data.</summary>
public sealed class BinaryNodeBytes(byte[] data) : BinaryNodeContent
{
    public byte[] Data { get; } = data;

    public override string ToString()
    {
        return "0x" + BitConverter.ToString(Data).Replace("-", string.Empty);
    }
}

/// <summary>Extension helpers for <see cref="BinaryNode"/>.</summary>
public static class BinaryNodeExtensions
{
    /// <summary>Returns all direct child nodes, or an empty sequence if there are none.</summary>
    public static IEnumerable<BinaryNode> GetChildren(this BinaryNode node)
        => node.Content is BinaryNodeList list ? list.Children : [];

    /// <summary>Returns the first child whose tag matches <paramref name="tag"/>.</summary>
    public static BinaryNode? GetChild(this BinaryNode node, string tag)
        => GetChildren(node).FirstOrDefault(c => c.Tag == tag);

    /// <summary>Returns the raw bytes of content, or null when there is no byte content.</summary>
    public static byte[]? GetBytes(this BinaryNode node)
        => node.Content is BinaryNodeBytes b ? b.Data : null;

    /// <summary>Returns the string value of content, or null when there is no string content.</summary>
    public static string? GetString(this BinaryNode node)
        => node.Content is BinaryNodeString s ? s.Value : null;
}
