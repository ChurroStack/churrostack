namespace ChurrOS.Api.Models.Dtos.Template.Definition
{
    public class ParameterOptionDefinition
    {
        /// <summary>
        /// Parameter option name (e.g. us-east-1)
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Parameter option title (e.g. US East 1)
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Optional parameter icon (e.g lucide:globe)
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Translation
        /// </summary>
        public IDictionary<string, string>? Translation { get; set; }

        public ParameterOptionDefinition(string value, string title, string? icon, IDictionary<string, string>? translation)
        {
            Value = value;
            Title = title;
            Icon = icon;
            Translation = translation;
        }
    }
}
