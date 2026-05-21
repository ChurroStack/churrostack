namespace ChurrOS.Api.Models.Dtos.Metrics
{
    public class MetricValuesItem
    {
        public string Name { get; private set; }
        public IDictionary<string, string> Labels { get; private set; }
        public MetricValueItem[] Values { get; private set; }

        public MetricValuesItem(string name, IDictionary<string, string> labels, MetricValueItem[] values)
        {
            Name = name;
            Labels = labels;
            Values = values;
        }
    }
}
