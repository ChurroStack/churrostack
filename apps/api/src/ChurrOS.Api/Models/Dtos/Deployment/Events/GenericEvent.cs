namespace ChurrOS.Api.Models.Dtos.Deployment.Events
{
    public class GenericEvent
    {
        public DateTimeOffset Timestamp { get; private set; }
        public string Reason { get; private set; }
        public string Type { get; private set; }
        public string Note { get; private set; }
        public string Kind { get; private set; }
        public string Name { get; private set; }
        public IDictionary<string, string> Annotations { get; private set; }

        public GenericEvent(DateTimeOffset timestamp, string reason, string type, string note, string kind, string name, IDictionary<string, string> annotations)
        {
            Timestamp = timestamp;
            Reason = reason;
            Type = type;
            Note = note;
            Kind = kind;
            Name = name;
            Annotations = annotations;
        }
    }
}
