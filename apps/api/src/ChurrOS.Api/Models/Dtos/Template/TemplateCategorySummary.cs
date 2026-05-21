namespace ChurrOS.Api.Models.Dtos.Template
{
    public class TemplateCategorySummary
    {
        public string Icon { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }

        public TemplateCategorySummary(string icon, string title, string description)
        {
            Icon = icon;
            Title = title;
            Description = description;
        }
    }
}
