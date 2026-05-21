namespace ChurrOS.Api.Models.Dtos.Template.Definition
{
    public class TemplateDefinition
    {
        /// <summary>
        /// Template name (e.g. com.churrostack.application.streamlit)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Template type (e.g. com.churrostack.environment.kubernetes)
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// Defines if this template is standalone or needs to be used as an extension in another template
        /// </summary>
        public TemplateType Type { get; set; }

        /// <summary>
        /// Template semantic version (e.g. 1.0.0)
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Apply order
        /// </summary>
        public int? Order { get; set; }

        /// <summary>
        /// Category icon (e.g lucide:database)
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Template title (e.g. Streamlit Application)
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Template description with markdown support
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Defines the category of the template
        /// </summary>
        public CategoryDefinition? Category { get; set; }

        /// <summary>
        /// Defines if this template can be added multiple times to an application
        /// </summary>
        public bool Singleton { get; set; }

        /// <summary>
        /// Configures the extensions associated with the template (e.g console, terminal, vscode)
        /// </summary>
        public ExtensionReferenceDefinition[]? Extensions { get; set; }

        /// <summary>
        /// Template port definitions
        /// </summary>
        public PortDefinition[]? Ports { get; set; }

        /// <summary>
        /// Bash script to be executed at startup
        /// </summary>
        public string? StartupScript { get; set; }

        /// <summary>
        /// Template input parameters
        /// </summary>
        public IDictionary<string, ParameterDefinition>? Parameters { get; set; }

        /// <summary>
        /// Template output metadata
        /// </summary>
        public IDictionary<string, MetadataDefinition>? Metadata { get; set; }

        /// <summary>
        /// Environment variables
        /// </summary>
        public IDictionary<string, string>? Environment { get; set; }

        /// <summary>
        /// Translation
        /// </summary>
        public IDictionary<string, string>? Translation { get; set; }

        public TemplateDefinition(string name, string version, CategoryDefinition? category, TemplateType type, string target, bool singleton, string title, string description, ExtensionReferenceDefinition[]? extensions, PortDefinition[]? ports, string? startupScript, IDictionary<string, ParameterDefinition>? parameters, IDictionary<string, MetadataDefinition>? metadata, IDictionary<string, string>? environment, IDictionary<string, string>? translation, string icon, int? order = null)
        {
            Name = name;
            Version = version;
            Category = category;
            Type = type;
            Target = target;
            Singleton = singleton;
            Title = title;
            Description = description;
            Extensions = extensions;
            Ports = ports;
            StartupScript = startupScript;
            Parameters = parameters;
            Metadata = metadata;
            Environment = environment;
            Translation = translation;
            Icon = icon;
            Order = order ?? 100;
        }
    }
}
