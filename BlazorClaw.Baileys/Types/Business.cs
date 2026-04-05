namespace Baileys.Types;

/// <summary>
/// Business types, mirroring the TypeScript <c>Types/Business.ts</c> interface.
/// </summary>

public enum DayOfWeekBusiness
{
    Sun, Mon, Tue, Wed, Thu, Fri, Sat
}

/// <summary>Specific open/close hours for a business day.</summary>
public sealed class BusinessHoursSpecific
{
    public DayOfWeekBusiness Day { get; init; }
    public string Mode => "specific_hours";
    public required string OpenTimeInMinutes { get; init; }
    public required string CloseTimeInMinutes { get; init; }
}

/// <summary>A business day that is either open 24h or appointment-only.</summary>
public sealed class BusinessHoursAllDay
{
    public DayOfWeekBusiness Day { get; init; }
    /// <summary>"open_24h" or "appointment_only".</summary>
    public required string Mode { get; init; }
}

/// <summary>Properties for updating a WhatsApp Business profile.</summary>
public sealed class UpdateBusinessProfileProps
{
    public string? Address { get; init; }
    public IReadOnlyList<string>? Websites { get; init; }
    public string? Email { get; init; }
    public string? Description { get; init; }
    public BusinessHoursConfig? Hours { get; init; }
}

/// <summary>Business hours configuration.</summary>
public sealed class BusinessHoursConfig
{
    public required string Timezone { get; init; }
    public IReadOnlyList<object>? Days { get; init; }
}

/// <summary>
/// A WhatsApp Business profile, mirroring the TypeScript
/// <c>Types/index.ts</c> WABusinessProfile type.
/// </summary>
public sealed class WaBusinessProfile
{
    public required string Description { get; init; }
    public string? Email { get; init; }
    public WaBusinessHours BusinessHours { get; init; } = new();
    public IReadOnlyList<string> Website { get; init; } = [];
    public string? Category { get; init; }
    public string? Wid { get; init; }
    public string? Address { get; init; }
}

/// <summary>Business hours object as returned by WhatsApp.</summary>
public sealed class WaBusinessHours
{
    public string? Timezone { get; init; }
    public IReadOnlyList<WaBusinessHoursConfig>? Config { get; init; }
    public IReadOnlyList<WaBusinessHoursConfig>? BusinessConfig { get; init; }
}

/// <summary>A single day entry in a business hours configuration.</summary>
public sealed class WaBusinessHoursConfig
{
    public required string DayOfWeek { get; init; }
    public required string Mode { get; init; }
    public int? OpenTime { get; init; }
    public int? CloseTime { get; init; }
}
