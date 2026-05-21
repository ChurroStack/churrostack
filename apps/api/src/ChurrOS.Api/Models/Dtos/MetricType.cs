using ChurrOS.Api.Utils.Converters;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos
{
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum MetricType : byte
    {
        [EnumMember(Value = "counter")] Counter = 0,
        [EnumMember(Value = "gauge")] Gauge = 1,
        [EnumMember(Value = "histogram")] Histogram = 2,
        [EnumMember(Value = "summary")] Summary = 3
    }
}
