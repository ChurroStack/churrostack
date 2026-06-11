namespace ChurrOS.Api.Models.Dtos.Environment
{
    public class EnvironmentDefinition
    {
        public string Name { get; set; }

        public string BasePath { get; set; }

        /// <summary>
        /// Environment name (e.g. com.churrostack.environment.kubernetes)
        /// </summary>
        public string Type => "com.churrostack.environment.kubernetes";

        /// <summary>
        /// Capabilities of the environment, (e.g service_mesh: istio, user_storage_class: emptyDir, shared_storage_class: hostpath)
        /// </summary>
        public Dictionary<string, string> Capabilities { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Environment description with markdown support
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Size resource limits
        /// </summary>
        public EnvironmentQuotaDefinition? Limits { get; set; }

        /// <summary>
        /// Environment available sizes
        /// </summary>
        public EnvironmentSizeDefinition[]? Sizes { get; set; }

        /// <summary>
        /// Host paths the environment exposes for "Map to" storage mounts.
        /// </summary>
        public EnvironmentHostPathDefinition[]? HostPaths { get; set; }

        /// <summary>
        /// Translation
        /// </summary>
        public IDictionary<string, string>? Translation { get; set; }

        public EnvironmentDefinition() : this(string.Empty, string.Empty, new Dictionary<string, string>(), string.Empty, null, null, null)
        {
        }

        public EnvironmentDefinition(string name, string basePath, Dictionary<string, string> capabilities, string description, EnvironmentQuotaDefinition? limits, EnvironmentSizeDefinition[]? sizes, IDictionary<string, string>? translation)
        {
            Name = name;
            BasePath = basePath;
            Capabilities = capabilities;
            Description = description;
            Limits = limits;
            Sizes = sizes;
            Translation = translation;
        }
    }
}
