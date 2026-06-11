namespace ChurrOS.Api.Models.Dtos.Environment
{
    /// <summary>
    /// A host path the environment exposes for "Map to" storage mounts. Mirrors the
    /// runner's HostPathDefinition. <see cref="Allowed"/> lists the identity names
    /// and/or group names permitted to use the path; access is enforced by the API.
    /// </summary>
    public class EnvironmentHostPathDefinition
    {
        public string Path { get; set; } = string.Empty;

        public string? Title { get; set; }

        public string[] Allowed { get; set; } = [];
    }
}
