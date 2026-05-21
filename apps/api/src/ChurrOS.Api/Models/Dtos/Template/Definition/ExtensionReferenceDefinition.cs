namespace ChurrOS.Api.Models.Dtos.Template.Definition
{
    public class ExtensionReferenceDefinition
    {
        /// <summary>
        /// Extension name (e.g. console)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Extension template (e.g. com.churrostack.extension.console)
        /// </summary>
        public string Template { get; set; }

        /// <summary>
        /// The extension is required for the template to function
        /// </summary>
        public bool Required { get; set; }

        public bool? Enabled { get; set; }

        public IDictionary<string, object>? Parameters { get; set; }

        public ExtensionReferenceDefinition(string name, string template, bool required, bool? enabled, IDictionary<string, object>? parameters)
        {
            Name = name;
            Template = template;
            Required = required;
            Parameters = parameters;
            Enabled = enabled;
        }
    }
}
