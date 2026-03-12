using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Core.Models;

public class ChannelConfig
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public required string ChannelType { get; set; } // "Telegram", "Matrix"

    [Required]
    [MaxLength(100)]
    public required string ChannelId { get; set; } // z.B. "default", "alerts"

    [MaxLength(500)]
    public string? Token { get; set; } // Bot Token oder Access Token

    [MaxLength(500)]
    public string? ApiKey { get; set; } // API Key für Matrix

    [MaxLength(200)]
    public string? Homeserver { get; set; } // Matrix Homeserver URL

    [MaxLength(100)]
    public string? UserId { get; set; } // Matrix User ID

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
