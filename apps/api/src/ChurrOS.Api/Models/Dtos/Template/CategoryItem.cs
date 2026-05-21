namespace ChurrOS.Api.Models.Dtos.Template
{
    public class CategoryItem
    {
        /// <summary>
        /// Category name (e.g. databases)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Category title (e.g. Databases)
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Category icon (e.g lucide:database)
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Translation
        /// </summary>
        public IDictionary<string, string>? Translation { get; set; }

        public CategoryItem(string name, string title, string? icon = null, IDictionary<string, string>? translation = null)
        {
            Name = name;
            Title = title;
            Icon = icon ?? "lucide:group";
            Translation = translation;
        }
    }
}
