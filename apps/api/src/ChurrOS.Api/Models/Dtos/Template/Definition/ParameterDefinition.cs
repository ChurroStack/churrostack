using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Template.Definition
{
    public class ParameterDefinition
    {
        /// <summary>
        /// Parameter title (e.g. CPU Usage)
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Parameter type (string, number, boolean, list)
        /// </summary>
        public ParameterType Type { get; set; }

        /// <summary>
        /// Optional parameter icon (e.g lucide:table-of-contents)
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Optional parameter description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Parameter default value
        /// </summary>
        [JsonPropertyName("default_value")]
        public string[]? DefaultValue { get; set; }

        /// <summary>
        /// Parameter UI hint
        /// </summary>
        [JsonPropertyName("ui_hint")]
        public string? UiHint { get; set; }

        /// <summary>
        /// Set to true if the parameter must be provided
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Set to true if the parameter accepts multiple values
        /// </summary>
        public bool Multi { get; set; }

        /// <summary>
        /// Condition to show the parameter
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// Value transformer helper
        /// </summary>
        public string? Transformer { get; set; }

        /// <summary>
        /// Translation
        /// </summary>
        public IDictionary<string, string>? Translation { get; set; }

        /// <summary>
        /// Parameter options when type is list
        /// </summary>
        public ParameterOptionDefinition[]? Options { get; set; }

        public ParameterDefinition(string title, ParameterType type, string? icon, string description, string uiHint, IDictionary<string, string>? translation, ParameterOptionDefinition[]? options, bool required, bool multi, string[]? defaultValue, string? condition = null, string? transformer = null)
        {
            Title = title;
            Type = type;
            Icon = icon ?? "lucide:table-of-contents";
            Translation = translation;
            Description = description;
            Options = options;
            Required = required;
            Multi = multi;
            DefaultValue = defaultValue;
            Condition = condition ?? "true";
            Transformer = transformer;
            UiHint = uiHint;
        }
    }
}
