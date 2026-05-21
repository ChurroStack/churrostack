using System.ComponentModel.DataAnnotations;

namespace ChurrunKubernetes.Domain
{
    public class Template
    {
        [Required]
        public long Id { get; protected set; }

        [Required]
        public string Name { get; private set; }

        [Required]
        public byte[] Hash { get; private set; }

        [MaxLength]
        public string Content { get; private set; }

        [Required]
        public DateTime CreatedOn { get; protected set; }

        public Template(long id, string name, byte[] hash, string content, DateTime createdOn)
        {
            Id = id;
            Name = name;
            Hash = hash;
            Content = content;
            CreatedOn = createdOn;
        }
    }
}
