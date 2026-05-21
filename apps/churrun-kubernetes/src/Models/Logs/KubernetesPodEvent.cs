namespace ChurrunKubernetes.Models.Logs
{
    public class KubernetesPodEvent
    {
        public DateTimeOffset Timestamp { get; private set; }
        public string Kind { get; private set; }
        public string Name { get; private set; }
        public string Status { get; private set; }
        public string Age { get; private set; }
        public IDictionary<string, string> Annotations { get; private set; }

        public KubernetesPodEvent(DateTimeOffset timestamp, string kind, string name, string status, string age, IDictionary<string, string> annotations)
        {
            Timestamp = timestamp;
            Kind = kind;
            Name = name;
            Status = status;
            Age = age;
            Annotations = annotations;
        }
    }

}
