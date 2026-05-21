using ChurrOS.Api.Utils;
using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Domain
{
    public class MetricValue
    {
        [HypertablePartitionColumn]
        public long AccountId { get; set; }

        [Required]
        public long MetricId { get; set; }

        [HypertableColumn]
        public DateTimeOffset Timestamp { get; set; }

        public double Value { get; set; }

        public MetricValue(long accountId, long metricId, DateTimeOffset timestamp, double value)
        {
            AccountId = accountId;
            MetricId = metricId;
            Timestamp = timestamp;
            Value = value;
        }
    }
}
