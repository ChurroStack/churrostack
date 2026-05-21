namespace ChurrunKubernetes.Models.Dtos.Template
{
    public class TemplateSummary
    {
        public string Name { get; set; }
        public byte[] Hash { get; set; }
        public string Content { get; set; }
        public DateTime CreatedOn { get; set; }

        public TemplateSummary(string name, byte[] hash, string content, DateTime createdOn)
        {
            Name = name;
            Hash = hash;
            Content = content;
            CreatedOn = createdOn;
        }
    }
}
