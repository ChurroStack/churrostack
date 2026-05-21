using ChurrOS.Api.Models.Dtos;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Domain
{
    [PrimaryKey(nameof(AccountId), nameof(Hash))]
    public class Metric
    {
        [Required]
        public long AccountId { get; protected set; }
        public virtual Account? Account { get; protected set; }

        [Required]
        public byte[] Hash { get; set; }

        [Required]
        public long MetricId { get; protected set; }

        public MetricType Type { get; protected set; }

        [Required]
        public IDictionary<string, string> Labels { get; protected set; }

        public Metric(long accountId, byte[] hash, long metricId, MetricType type, IDictionary<string, string> labels)
        {
            AccountId = accountId;
            Hash = hash;
            MetricId = metricId;
            Type = type;
            Labels = labels;
        }
    }
}
