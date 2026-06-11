namespace ChurrOS.Api.Models.Dtos.Environment
{
    /// <summary>
    /// A host path the current user is allowed to use, returned to the UI "Map to"
    /// dropdown. The environment's allow-list is never exposed — only the resulting
    /// permitted paths.
    /// </summary>
    public class AllowedHostPathItem
    {
        public string Path { get; set; }

        public string? Title { get; set; }

        public AllowedHostPathItem(string path, string? title)
        {
            Path = path;
            Title = title;
        }
    }
}
