namespace ChurrOS.Api.Models.Dtos.Gallery
{
    public class GalleryAppSummary
    {
        public string? Icon { get; protected set; }
        public string Name { get; protected set; }
        public string? Type { get; protected set; }
        public string Description { get; protected set; }
        public string? Path { get; protected set; }
        public string[] Tags { get; protected set; }

        public GalleryAppSummary(string? icon, string name, string? type, string description, string? path, string[]? tags = null)
        {
            Icon = icon;
            Name = name;
            Type = type;
            Description = description;
            Path = path;
            Tags = tags ?? Array.Empty<string>();
        }
    }
}
