using Microsoft.AspNetCore.Identity;

namespace BlazorClaw.Server.Data;

public class ApplicationRole : IdentityRole
{
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}