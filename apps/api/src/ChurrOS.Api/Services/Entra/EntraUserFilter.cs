using ChurrOS.Api.Models.Dtos.IdentitySync;

namespace ChurrOS.Api.Services.Entra
{
    public static class EntraUserFilter
    {
        /// <summary>
        /// Splits "A, B ,C" into trimmed, non-empty values.
        /// </summary>
        public static string[] ParseValues(string? commaSeparated) =>
            (commaSeparated ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();

        /// <summary>
        /// (prop1 = A OR B OR C) AND (prop2 = A OR D). Case-insensitive, trimmed; a missing/empty property never matches.
        /// A disabled (or rule-less) filter matches everything.
        /// </summary>
        public static bool Matches(IReadOnlyDictionary<string, string?> properties, EntraFilter? filter)
        {
            if (filter is null || !filter.Enabled || filter.Rules.Count == 0)
                return true;

            foreach (var rule in filter.Rules)
            {
                if (!properties.TryGetValue(rule.Property, out var actual) || string.IsNullOrWhiteSpace(actual))
                    return false;

                var actualTrimmed = actual.Trim();
                var anyValue = rule.Values.Any(v => string.Equals(v?.Trim(), actualTrimmed, StringComparison.OrdinalIgnoreCase));
                if (!anyValue)
                    return false;
            }

            return true;
        }
    }
}
