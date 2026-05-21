namespace ChurrOS.Api.Models.Dtos.Gallery
{
    public class GalleryLlmSummary
    {
        public string? Icon { get; protected set; }
        public string[] Names { get; protected set; }

        public GalleryLlmSummary(string? icon, string[] names)
        {
            Icon = icon;
            Names = names;
        }
    }
}
