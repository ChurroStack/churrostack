using System.Text.RegularExpressions;

namespace ChurrOS.Api.Utils
{
    public static class TagsHelper
    {
        private static readonly Regex TagPattern = new(@"^[a-z0-9_-]{1,32}$", RegexOptions.Compiled);

        /// <summary>
        /// Trims, lowercases, dedupes and validates tag strings. Throws
        /// <see cref="ArgumentException"/> if any tag fails the validation regex.
        /// </summary>
        public static string[] Normalize(string[]? raw)
        {
            if (raw is null) return Array.Empty<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>(raw.Length);
            foreach (var item in raw)
            {
                if (string.IsNullOrWhiteSpace(item)) continue;
                var normalized = item.Trim().ToLowerInvariant();
                if (!TagPattern.IsMatch(normalized))
                    throw new ArgumentException($"Tag '{item}' (normalized: '{normalized}') is invalid. Tags must match [a-z0-9_-]{{1,32}}.");
                if (seen.Add(normalized))
                    result.Add(normalized);
            }
            return result.ToArray();
        }
    }
}
