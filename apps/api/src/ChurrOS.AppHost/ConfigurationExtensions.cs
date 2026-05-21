using Microsoft.Extensions.Configuration;

namespace ChurrOS.AppHost
{
    internal static class ConfigurationExtensions
    {
        public static IResourceBuilder<T> WithConfiguration<T>(this IResourceBuilder<T> builder, string rootKey, IDictionary<string, string>? replace = null) where T : IResourceWithEnvironment
        {
            var targetedConfiguration = builder.ApplicationBuilder.Configuration.GetSection(rootKey);
            var configValues = new Dictionary<string, string>();
            LoadConfigurationSectionsAsKeyValuePairs(targetedConfiguration.GetChildren(), configValues, rootKey);
            if (configValues.Keys is null || configValues.Keys.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(builder.ApplicationBuilder.Configuration[rootKey]))
                {
                    return builder;
                }
                return builder.WithEnvironment(rootKey, builder.ApplicationBuilder.Configuration[rootKey]);
            }
            foreach (var key in configValues.Keys)
            {
                var value = configValues[key];
                if (replace != null && replace.TryGetValue(key, out var replacedValue))
                {
                    continue;
                }
                builder = builder.WithEnvironment(key, configValues[key]);
            }
            if (replace is not null)
            {
                foreach (var value in replace)
                {
                    builder = builder.WithEnvironment(value.Key, value.Value);
                }
            }
            return builder;
        }

        private static void LoadConfigurationSectionsAsKeyValuePairs(IEnumerable<IConfigurationSection> sections, IDictionary<string, string> keyValuePairs, string prefix)
        {
            foreach (var section in sections)
            {
                var key = string.IsNullOrEmpty(prefix) ? section.Key : $"{prefix}__{section.Key}";

                // If the section has a value, it's a leaf node, add it to the dictionary
                if (section.Value != null)
                    keyValuePairs[key] = section.Value;
                else
                    // If the section has children, recursively process them
                    LoadConfigurationSectionsAsKeyValuePairs(section.GetChildren(), keyValuePairs, key);
            }
        }

    }
}
