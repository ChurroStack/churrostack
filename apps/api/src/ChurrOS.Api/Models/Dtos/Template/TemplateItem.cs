using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Template.Definition;

namespace ChurrOS.Api.Models.Dtos.Template
{
    public class TemplateItem
    {
        /// <summary>
        /// Name (e.g. com.churrostack.application.streamlit)
        /// </summary>
        public string Name { get; private set; }

        public byte[] Hash { get; private set; }

        /// <summary>
        /// Category icon (e.g lucide:database)
        /// </summary>
        public string Icon { get; private set; }

        /// <summary>
        /// Template title (e.g. Streamlit Application)
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// Template description with markdown support
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Defines the category of the template
        /// </summary>
        public TemplateCategorySummary? Category { get; private set; }

        /// <summary>
        /// Defines if this template is standalone or needs to be used as an extension in another template
        /// </summary>
        public string Type { get; private set; }

        public string Target { get; private set; }

        public TemplateDefinition Definition { get; private set; }

        public string Content { get; private set; }

        public DateTimeOffset CreatedAt { get; protected set; }
        public IdentitySummary CreatedBy { get; protected set; }
        public DateTimeOffset ModifiedAt { get; protected set; }
        public IdentitySummary ModifiedBy { get; protected set; }

        public TemplateItem(string name, string icon, string title, string description, TemplateCategorySummary? category, string type, TemplateDefinition definition, string content, byte[] hash, string target, DateTimeOffset createdAt, IdentitySummary createdBy, DateTimeOffset modifiedAt, IdentitySummary modifiedBy)
        {
            Name = name;
            Icon = icon;
            Title = title;
            Description = description;
            Category = category;
            Type = type;
            Definition = definition;
            Content = content;
            Hash = hash;
            Target = target;
            CreatedAt = createdAt;
            CreatedBy = createdBy;
            ModifiedAt = modifiedAt;
            ModifiedBy = modifiedBy;
        }
    }
}
