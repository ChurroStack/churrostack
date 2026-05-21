namespace ChurrOS.Api.Models.Dtos.Template.Definition
{
    public class MetadataDefinition
    {
        /// <summary>
        /// Metadata name (e.g. cpu_usage)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Metadata title (e.g. CPU Usage)
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Runner type (e.g. prometheus, bash, python)
        /// </summary>
        public string Runner { get; set; }

        /// <summary>
        /// Metadata formula (e.g sh script to get cpu_usage)
        /// </summary>
        public string Formula { get; set; }

        /// <summary>
        /// Translation
        /// </summary>
        public IDictionary<string, string>? Translation { get; set; }

        public MetadataDefinition(string name, string title, string runner, string formula, IDictionary<string, string>? translation)
        {
            Name = name;
            Title = title;
            Runner = runner;
            Formula = formula;
            Translation = translation;
        }
    }
}
