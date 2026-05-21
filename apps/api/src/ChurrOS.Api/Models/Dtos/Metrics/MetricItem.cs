namespace ChurrOS.Api.Models.Dtos.Metrics
{
    public class MetricItem
    {
        public long AccountId { get; protected set; }
        public DateTimeOffset Timestamp { get; protected set; }
        public double Value { get; protected set; }
        public MetricType? Type { get; protected set; }
        public IDictionary<string, string> Labels { get; protected set; }

        public MetricItem(long accountId, IDictionary<string, string> labels, DateTimeOffset timestamp, double value, MetricType? type)
        {
            AccountId = accountId;
            Labels = labels ?? new Dictionary<string, string>();
            Timestamp = timestamp;
            Value = value;
            Type = type;
        }
    }
}
