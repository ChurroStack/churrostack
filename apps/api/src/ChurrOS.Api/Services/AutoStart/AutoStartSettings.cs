using System.Text.Json;

namespace ChurrOS.Api.Services.AutoStart
{
    public sealed record AutoStartSettings(bool AutoStartEnabled, bool AutoStopEnabled, int IdleMinutes)
    {
        public const int DefaultIdleMinutes = 60;

        public static readonly AutoStartSettings Disabled = new(false, false, DefaultIdleMinutes);

        public static AutoStartSettings From(JsonElement? metadata)
        {
            if (metadata is null || metadata.Value.ValueKind != JsonValueKind.Object)
                return Disabled;

            var autoStart = TryReadBool(metadata.Value, "autoStart", "enabled");
            var autoStop = TryReadBool(metadata.Value, "autoStop", "enabled");
            var idle = TryReadInt(metadata.Value, "autoStop", "idleMinutes") ?? DefaultIdleMinutes;
            if (idle <= 0) idle = DefaultIdleMinutes;

            return new AutoStartSettings(autoStart, autoStop, idle);
        }

        private static bool TryReadBool(JsonElement root, string outer, string inner)
        {
            if (!root.TryGetProperty(outer, out var section) || section.ValueKind != JsonValueKind.Object)
                return false;
            if (!section.TryGetProperty(inner, out var value))
                return false;
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(value.GetString(), out var b) && b,
                _ => false
            };
        }

        private static int? TryReadInt(JsonElement root, string outer, string inner)
        {
            if (!root.TryGetProperty(outer, out var section) || section.ValueKind != JsonValueKind.Object)
                return null;
            if (!section.TryGetProperty(inner, out var value))
                return null;
            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt32(out var i) => i,
                JsonValueKind.String when int.TryParse(value.GetString(), out var i) => i,
                _ => null
            };
        }
    }
}
