namespace BlazorClaw.Core.Security
{
    public class SecurityOptions
    {
        public const string Section = "Security";
        public string DefaultGroup { get; set; } = "user";
        public Dictionary<string, UserGroup> UserGroups { get; set; } = [];
        public Dictionary<string, ToolPolicy> ToolPolicies { get; set; } = [];
    }

    public class UserGroup
    {
        public List<string> AllowedTools { get; set; } = [];
        public SandboxConfig Sandbox { get; set; } = new();
        public List<RateLimit> RateLimits { get; set; } = [];
    }

    public class SandboxConfig
    {
        public bool Enabled { get; set; } = true;
        public List<string> Paths { get; set; } = [];
    }

    public class ToolPolicy
    {
        public long? MaxFileSize { get; set; }
        public int? Timeout { get; set; }
        public int? MaxConcurrent { get; set; }

        public List<string> AllowedPaths { get; set; } = [];
        public List<string> DeniedPaths { get; set; } = [];

        public bool AuditLog { get; set; }
        public bool NotifyUser { get; set; }

        public List<RateLimit> RateLimits { get; set; } = [];
    }

    public class RateLimit
    {
        public string? GroupName { get; set; }
        public int? MaxRequests { get; set; }
        public int? MaxTokens { get; set; }
        public TimeSpan TimeSpan { get; set; }
        public bool OnlyCountSuccess { get; set; } = true;
    }
}
