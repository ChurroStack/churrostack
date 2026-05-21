using ChurrOS.Api.Models.Dtos.Share;

namespace ChurrOS.Api.Models.Dtos.Template.Definition
{
    public class PortDefinitionItem
    {
        /// <summary>
        /// Port name (e.g. web, console, desktop)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Port title (e.g. HTTP, HTTPS, Custom Application Port)
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Port icon (e.g lucide:globe for Web HTTP port)
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Port description with markdown support
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Port type
        /// </summary>
        public ProtocolType Protocol { get; set; }

        /// <summary>
        /// Launc Uri when protocol is HTTP
        /// </summary>
        public string? Uri { get; set; }

        /// <summary>
        /// Authentication mode
        /// </summary>
        public AuthenticationMode Authentication { get; set; }

        public SharingMode Sharing { get; set; }

        public int? Port { get; set; }

        /// <summary>
        /// Translation
        /// </summary>
        public IDictionary<string, string>? Translation { get; set; }

        public PortDefinitionItem(string name, string title, string icon, string description, ProtocolType protocol, string? uri, AuthenticationMode authentication, SharingMode sharing, int? port, IDictionary<string, string>? translation)
        {
            Name = name;
            Title = title;
            Icon = icon ?? "lucide:ethernet-port";
            Description = description;
            Protocol = protocol;
            Uri = uri;
            Authentication = authentication;
            Sharing = sharing;
            Port = port;
            Translation = translation;
        }
    }
}
