using ChurrOS.Api.Models.Dtos.Share;

namespace ChurrOS.Api.Models.Dtos.Template.Definition
{
    public class PortDefinition
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
        /// Port number
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Port type
        /// </summary>
        public ProtocolType Protocol { get; set; }

        /// <summary>
        /// Launc Uri when protocol is HTTP
        /// </summary>
        public string? Uri { get; set; }

        public IList<IDictionary<string, string>>? Transforms { get; set; }

        /// <summary>
        /// Authentication mode
        /// </summary>
        public AuthenticationMode Authentication { get; set; }

        public SharingMode Sharing { get; set; }

        /// <summary>
        /// Translation
        /// </summary>
        public IDictionary<string, string>? Translation { get; set; }

        public PortDefinition(string name, string title, string icon, string description, int port, ProtocolType protocol, string? uri, IList<IDictionary<string, string>>? transforms, AuthenticationMode authentication, SharingMode sharing, IDictionary<string, string>? translation)
        {
            Name = name;
            Title = title;
            Icon = icon ?? "lucide:ethernet-port";
            Description = description;
            Port = port;
            Protocol = protocol;
            Uri = uri;
            Transforms = transforms;
            Authentication = authentication;
            Sharing = sharing;
            Translation = translation;
        }
    }
}
