namespace ChurrunKubernetes.Models.Dtos.Environment
{
    public class SizeDefinition
    {
        /// <summary>
        /// Size name (e.g. 1x2gb, 9x32gb)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Size title (e.g. Small 1 vCPU 2 GB RAM, Medium, Large)
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Size description with markdown support
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Size resource requests
        /// </summary>
        public QuotaDefinition? Requests { get; set; }

        /// <summary>
        /// Size resource limits
        /// </summary>
        public QuotaDefinition? Limits { get; set; }

        /// <summary>
        /// Translation
        /// </summary>
        public IDictionary<string, string>? Translation { get; set; }

        public SizeDefinition(string name, string title, string? description, QuotaDefinition? requests, QuotaDefinition? limits, IDictionary<string, string>? translation)
        {
            Name = name;
            Title = title;
            Description = description;
            Requests = requests;
            Limits = limits;
            Translation = translation;
        }
    }
}
