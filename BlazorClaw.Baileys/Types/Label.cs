namespace Baileys.Types;

// ──────────────────────────────────────────────────────────────────────────────
//  Label
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Represents a WhatsApp label (chat/business label),
/// mirroring the TypeScript <c>Types/Label.ts</c> interface.
/// </summary>
public sealed class Label
{
    /// <summary>Unique label ID.</summary>
    public required string Id { get; init; }

    /// <summary>Display name of the label.</summary>
    public required string Name { get; init; }

    /// <summary>Color index (0–19).</summary>
    public int Color { get; init; }

    /// <summary>Whether the label has been deleted.</summary>
    public bool Deleted { get; init; }

    /// <summary>Predefined label ID (WhatsApp has 5 predefined labels).</summary>
    public string? PredefinedId { get; init; }
}

/// <summary>Body used when adding or removing a label.</summary>
public sealed class LabelActionBody
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public int? Color { get; init; }
    public bool? Deleted { get; init; }
    public int? PredefinedId { get; init; }
}

/// <summary>WhatsApp provides 20 predefined label colors.</summary>
public enum LabelColor
{
    Color1 = 0, Color2, Color3, Color4, Color5,
    Color6, Color7, Color8, Color9, Color10,
    Color11, Color12, Color13, Color14, Color15,
    Color16, Color17, Color18, Color19, Color20
}

// ──────────────────────────────────────────────────────────────────────────────
//  LabelAssociation
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>The type of object a label is associated with.</summary>
public enum LabelAssociationType
{
    /// <summary>The label is applied to a chat.</summary>
    Chat,
    /// <summary>The label is applied to a specific message.</summary>
    Message
}

/// <summary>Label associated with a chat.</summary>
public sealed class ChatLabelAssociation
{
    public LabelAssociationType Type => LabelAssociationType.Chat;
    public required string ChatId { get; init; }
    public required string LabelId { get; init; }
}

/// <summary>Label associated with a specific message.</summary>
public sealed class MessageLabelAssociation
{
    public LabelAssociationType Type => LabelAssociationType.Message;
    public required string ChatId { get; init; }
    public required string MessageId { get; init; }
    public required string LabelId { get; init; }
}

/// <summary>Body for add/remove chat label association actions.</summary>
public sealed class ChatLabelAssociationActionBody
{
    public required string LabelId { get; init; }
}

/// <summary>Body for add/remove message label association actions.</summary>
public sealed class MessageLabelAssociationActionBody
{
    public required string LabelId { get; init; }
    public required string MessageId { get; init; }
}
