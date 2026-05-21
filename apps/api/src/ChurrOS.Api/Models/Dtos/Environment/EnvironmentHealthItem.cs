namespace ChurrOS.Api.Models.Dtos.Environment
{
    public class EnvironmentHealthItem
    {
        public bool Healthy { get; private set; }
        public DateTimeOffset Timestamp { get; private set; }
        public string? Error { get; private set; }

        public EnvironmentHealthItem(bool healthy, DateTimeOffset timestamp, string? error)
        {
            Healthy = healthy;
            Timestamp = timestamp;
            Error = error;
        }
    }
}
