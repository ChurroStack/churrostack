using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Llm
{
    public class OaiModel
    {
        public string Id { get; private set; }
        public string Object { get; private set; }
        public long Created { get; private set; }
        [JsonPropertyName("owned_by")]
        public string OwnedBy { get; private set; }

        public OaiModel(string id, string @object, long created, string ownedBy)
        {
            Id = id;
            Object = @object;
            Created = created;
            OwnedBy = ownedBy;
        }
    }
}
