namespace ChurrOS.Api.Models.Dtos
{
    public class GitRepositoryItem
    {
        public string Url { get; private set; }
        public string[] Branches { get; private set; }

        public GitRepositoryItem(string url, string[] branches)
        {
            Url = url;
            Branches = branches;
        }
    }
}
