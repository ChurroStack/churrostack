using ChurrOS.Api.Models.Dtos.IdentitySync;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace ChurrOS.Api.Services.Entra
{
    /// <summary>Reads/writes <see cref="EntraSyncConfig"/> inside <c>Account.Metadata["entraSync"]</c> without disturbing other keys (e.g. quotas).</summary>
    public static class EntraSyncConfigStore
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public static EntraSyncConfig Read(IDictionary<string, object>? metadata)
        {
            if (metadata is null || !metadata.TryGetValue(EntraSyncConfig.MetadataKey, out var raw) || raw is null)
                return new EntraSyncConfig();

            var json = raw is JsonElement element ? element.GetRawText() : JsonSerializer.Serialize(raw, Options);
            return JsonSerializer.Deserialize<EntraSyncConfig>(json, Options) ?? new EntraSyncConfig();
        }

        public static IDictionary<string, object> Merge(IDictionary<string, object>? metadata, EntraSyncConfig config)
        {
            var merged = metadata is null ? new Dictionary<string, object>() : new Dictionary<string, object>(metadata);
            merged[EntraSyncConfig.MetadataKey] = JsonSerializer.SerializeToElement(config, Options);
            return merged;
        }

        /// <summary>Operator-set, per-account Entra tenant: <c>Account.Metadata["entraTenantId"]</c> (written directly in the account entry, never through the API).</summary>
        public const string TenantMetadataKey = "entraTenantId";

        private static readonly Regex TenantIdPattern = new("^[A-Za-z0-9.\\-]{1,128}$", RegexOptions.Compiled);

        /// <summary>
        /// The tenant the sync may read from: the account's own <c>entraTenantId</c> when the operator set one, otherwise the
        /// server default <c>ExternalProviders:Microsoft:TenantId</c>. Never taken from a request: the app secret is shared by the
        /// whole installation, so a user-supplied tenant would let an account admin read another organisation's directory.
        /// A value present on the account but malformed is a misconfiguration (null), it does NOT silently fall back to the default.
        /// </summary>
        public static string? ResolveTenantId(IDictionary<string, object>? accountMetadata, IConfiguration configuration)
        {
            if (accountMetadata is not null && accountMetadata.TryGetValue(TenantMetadataKey, out var raw) && raw is not null)
            {
                var value = (raw is JsonElement element ? (element.ValueKind == JsonValueKind.String ? element.GetString() : null) : raw.ToString())?.Trim();
                if (!string.IsNullOrEmpty(value))
                    return TenantIdPattern.IsMatch(value) ? value : null;
            }

            var fallback = configuration["ExternalProviders:Microsoft:TenantId"]?.Trim();
            return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
        }

        /// <summary>App registration (ClientId/ClientSecret) is configured and a tenant resolves for this account.</summary>
        public static bool IsAvailable(IDictionary<string, object>? accountMetadata, IConfiguration configuration) =>
            GraphDirectoryClient.IsConfigured(configuration) && ResolveTenantId(accountMetadata, configuration) is not null;

        public static string StatusKey(long accountId) => $"churros_tenant:{accountId}:entra_sync:status";

        public static string CancelKey(long accountId) => $"churros_tenant:{accountId}:entra_sync:cancel";

        public static readonly TimeSpan StaleHeartbeat = TimeSpan.FromMinutes(5);

        /// <summary>Idle when nothing ran yet; a queued/running status whose heartbeat is stale (pod died) is reported as failed so a new run can start.</summary>
        public static EntraSyncStatus Normalize(EntraSyncStatus? status, DateTimeOffset now)
        {
            if (status is null) return new EntraSyncStatus { State = EntraSyncState.Idle };
            var active = status.State is EntraSyncState.Queued or EntraSyncState.Running;
            if (active && (status.HeartbeatAt is null || now - status.HeartbeatAt.Value > StaleHeartbeat))
            {
                status.State = EntraSyncState.Failed;
                status.Error = "Interrupted: the sync stopped reporting progress.";
                status.FinishedAt ??= status.HeartbeatAt;
            }
            return status;
        }

        public static bool IsActive(EntraSyncStatus status) => status.State is EntraSyncState.Queued or EntraSyncState.Running;

        public static string JobName(long accountId) => $"entra-sync-{accountId}";

        public static string CronTriggerName(long accountId) => $"entra-sync-{accountId}-cron";
    }
}
