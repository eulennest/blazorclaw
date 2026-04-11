using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Core.Models;

public class OAuthToken
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ServerId { get; set; }

    [Required]
    [MaxLength(8000)]
    public required string AccessToken { get; set; }

    [MaxLength(8000)]
    public string? RefreshToken { get; set; }

    public DateTime ExpiresAt { get; set; }

    [Required]
    [MaxLength(2000)]
    public required string Scope { get; set; }

    public OAuthServer Server { get; set; } = null!;
}
