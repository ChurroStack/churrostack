namespace ChurrunKubernetes.Models.Dtos.Environment
{
    /// <summary>
    /// A host path the environment exposes for "Map to" storage mounts. The
    /// <see cref="Allowed"/> list (ChurroStack identity names and/or group names)
    /// is enforced control-plane side by the API; the runner only validates that a
    /// requested host path is one of these managed paths.
    /// </summary>
    public class HostPathDefinition
    {
        /// <summary>
        /// Absolute local path on the cluster node (e.g. /mnt/data/shared).
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Optional display label shown in the UI dropdown.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Identity names and/or group names allowed to use this path. Empty means
        /// nobody is allowed.
        /// </summary>
        public string[] Allowed { get; set; } = [];
    }
}
