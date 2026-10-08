namespace ChurrOS.Api.Services.AutoStart
{
    public static class AutoStartConstants
    {
        public const double CpuActivityCores = 0.05;

        public static readonly TimeSpan HoldTimeout = TimeSpan.FromSeconds(300);
        public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

        // Must exceed both the per-env start lock TTL (120 s) in StartApplicationHandler
        // and the HoldTimeout (300 s) so a single-flight leader cannot expire mid-start
        // and let a second leader fire while followers are still holding.
        public static readonly TimeSpan InflightTtl = TimeSpan.FromSeconds(420);

        public static readonly TimeSpan CooldownTtl = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan RunningTtl = TimeSpan.FromHours(24);

        // Backoff window after an auto-start fails. While this key exists, held and new requests
        // short-circuit to a fast 503 instead of polling to the HoldTimeout, and no new leader is
        // claimed — so the env lock is not hammered. After it expires the next request retries.
        // Capacity failures (env at capacity / busy) retry quickly, because capacity can free in
        // seconds when another app auto-stops; any other failure (e.g. runner unreachable) backs
        // off longer so a persistently-broken start does not become a retry storm.
        public static readonly TimeSpan StartFailedTtl = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan StartFailedHardTtl = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan LastActivityTtl = TimeSpan.FromHours(48);
        public static readonly TimeSpan RouteCacheTtl = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan LastActivityThrottle = TimeSpan.FromSeconds(30);

        // Short-circuit window so the 5-min auto-stop cron does not re-issue Stop while
        // ScrapeDeploymentStateJob has not yet observed the runner-side transition.
        public static readonly TimeSpan AutoStopInflightTtl = TimeSpan.FromMinutes(5);

        public static string RouteCacheKey(string appName) => $"app:{appName}:share_route";
        public static string InflightKey(long appId) => $"app:{appId}:autostart_inflight";
        public static string AutoStopInflightKey(long appId) => $"app:{appId}:autostop_inflight";
        public static string CooldownKey(long appId) => $"app:{appId}:autostart_cooldown";
        public static string RunningKey(long appId) => $"app:{appId}:running";
        public static string LastActivityKey(long appId) => $"app:{appId}:last_activity";
        public static string StartFailedKey(long appId) => $"app:{appId}:start_failed";
    }
}
