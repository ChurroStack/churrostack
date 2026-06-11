using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Template;

namespace ChurrOS.Api.Models.Dtos.Application
{
    public class ApplicationExtensionItem
    {
        public string Name { get; protected set; }
        public bool Enabled { get; protected set; }
        public TemplateItem Template { get; protected set; }
        /// <summary>
        /// Template name supplied by the client for multi-instance extensions (e.g.
        /// additional storage rows like "storage-2" that are not declared by name in
        /// the application template definition). Used only on create to resolve the
        /// template; ignored when the extension name matches a definition entry.
        /// </summary>
        public string? TemplateName { get; protected set; }
        public IDictionary<string, string[]> Parameters { get; protected set; }
        public DateTimeOffset CreatedAt { get; protected set; }
        public IdentitySummary CreatedBy { get; protected set; }
        public DateTimeOffset ModifiedAt { get; protected set; }
        public IdentitySummary ModifiedBy { get; protected set; }

        public ApplicationExtensionItem(string name, bool enabled, TemplateItem template, IDictionary<string, string[]> parameters, DateTimeOffset createdAt, IdentitySummary createdBy, DateTimeOffset modifiedAt, IdentitySummary modifiedBy, string? templateName = null)
        {
            Name = name;
            Enabled = enabled;
            Template = template;
            Parameters = parameters;
            CreatedAt = createdAt;
            CreatedBy = createdBy;
            ModifiedAt = modifiedAt;
            ModifiedBy = modifiedBy;
            TemplateName = templateName;
        }
    }
}
