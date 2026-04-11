using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Core.Models;

public class ApiKey
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public required string Identifier { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    public Guid? OAuthTokenId { get; set; }

    [MaxLength(8000)]
    public string? Value { get; set; }

    [MaxLength(100)]
    public string? TokenType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public OAuthToken? OAuthToken { get; set; }
}
