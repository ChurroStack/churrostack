using System.Text.Json;

namespace ChurrOS.Api.Models.Dtos.Application
{
    public class ApplicationEventItem
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Target { get; set; }
        public string Type { get; set; }
        public string Reason { get; set; }
        public string Message { get; set; }
        public JsonElement? Tags { get; set; }

        public ApplicationEventItem(DateTimeOffset timestamp, string target, string type, string reason, string message, JsonElement? tags)
        {
            Timestamp = timestamp;
            Target = target;
            Type = type;
            Reason = reason;
            Message = message;
            Tags = tags;
        }
    }
}
