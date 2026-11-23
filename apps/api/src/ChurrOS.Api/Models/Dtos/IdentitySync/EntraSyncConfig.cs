namespace ChurrOS.Api.Models.Dtos.IdentitySync
{
    /// <summary>One filter row: the user property must equal ANY of <see cref="Values"/> (OR).</summary>
    public class EntraFilterRule
    {
        public string Property { get; set; } = string.Empty;
        public string[] Values { get; set; } = Array.Empty<string>();
    }

    /// <summary>When enabled, a user must satisfy ALL rules (AND) to be synced.</summary>
    public class EntraFilter
    {
        public bool Enabled { get; set; }
        public List<EntraFilterRule> Rules { get; set; } = new();
    }

    /// <summary>Per-account Entra sync settings, stored under <c>Account.Metadata["entraSync"]</c>.</summary>
    public class EntraSyncConfig
    {
        public const string MetadataKey = "entraSync";
        public const string DefaultCron = "0 * * * *";

        public bool Enabled { get; set; }
        public string? TenantId { get; set; }
        public string CronExpression { get; set; } = DefaultCron;
        public EntraFilter Filter { get; set; } = new();
    }

    public class EntraSyncConfigItem
    {
        public EntraSyncConfig Config { get; set; } = new();

        /// <summary>False when the Microsoft app registration (ExternalProviders:Microsoft) is not configured.</summary>
        public bool Available { get; set; }
    }

    public static class EntraSyncState
    {
        public const string Idle = "idle";
        public const string Queued = "queued";
        public const string Running = "running";
        public const string Succeeded = "succeeded";
        public const string Failed = "failed";
        public const string Cancelled = "cancelled";
    }

    /// <summary>Progress/outcome of the latest run, kept in Redis (see docs/concepts/identity-sync.md).</summary>
    public class EntraSyncStatus
    {
        public string RunId { get; set; } = string.Empty;
        public string State { get; set; } = EntraSyncState.Idle;
        public string? Phase { get; set; }
        public int Processed { get; set; }
        public int? Total { get; set; }
        public string? Trigger { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? HeartbeatAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public string? Error { get; set; }
        public string? Summary { get; set; }
        public List<string> Warnings { get; set; } = new();
    }
}
