using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Models.Dtos.Llm
{
    public class LLmDestinationItem
    {
        [Required]
        public LLmDestinationType Type { get; set; }

        [Required]
        public string Uri { get; set; }

        [Required]
        public string Model { get; set; }

        public string? ApiKey { get; set; }

        public IDictionary<string, string>? Headers { get; set; }

        public string? Patch { get; set; }

        public LLmDestinationItem(LLmDestinationType type, string uri, string model, string? apiKey, IDictionary<string, string>? headers, string? patch)
        {
            Type = type;
            Uri = uri;
            Model = model;
            ApiKey = apiKey;
            Headers = headers;
            Patch = patch;
        }
    }
}
