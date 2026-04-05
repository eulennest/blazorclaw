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
}

/// <summary>Discriminated union for <see cref="BinaryNode.Content"/>.</summary>
public abstract class BinaryNodeContent { }

/// <summary>Node content that is a list of child <see cref="BinaryNode"/> nodes.</summary>
public sealed class BinaryNodeList(IReadOnlyList<BinaryNode> children) : BinaryNodeContent
{
    public IReadOnlyList<BinaryNode> Children { get; } = children;
}

/// <summary>Node content that is a plain UTF-8 string.</summary>
public sealed class BinaryNodeString(string value) : BinaryNodeContent
{
    public string Value { get; } = value;
}

/// <summary>Node content that is raw binary data.</summary>
public sealed class BinaryNodeBytes(byte[] data) : BinaryNodeContent
{
    public byte[] Data { get; } = data;
}

/// <summary>Extension helpers for <see cref="BinaryNode"/>.</summary>
public static class BinaryNodeExtensions
{
    /// <summary>Returns all direct child nodes, or an empty sequence if there are none.</summary>
    public static IEnumerable<BinaryNode> GetChildren(BinaryNode node)
        => node.Content is BinaryNodeList list ? list.Children : [];

    /// <summary>Returns the first child whose tag matches <paramref name="tag"/>.</summary>
    public static BinaryNode? GetChild(BinaryNode node, string tag)
        => GetChildren(node).FirstOrDefault(c => c.Tag == tag);

    /// <summary>Returns the raw bytes of content, or null when there is no byte content.</summary>
    public static byte[]? GetBytes(BinaryNode node)
        => node.Content is BinaryNodeBytes b ? b.Data : null;

    /// <summary>Returns the string value of content, or null when there is no string content.</summary>
    public static string? GetString(BinaryNode node)
        => node.Content is BinaryNodeString s ? s.Value : null;
}
