namespace ChurrOS.Api.Models.Dtos.Application
{
    public class ApplicationUsageItem
    {
        public string IdentityName { get; set; }
        public string ApplicationName { get; set; }
        public string EnvironmentName { get; set; }
        public DateTimeOffset? From { get; protected set; }
        public DateTimeOffset? To { get; protected set; }
        public long Requests { get; protected set; }
        public long IncomingTraffic { get; protected set; }
        public long OutgoingTraffic { get; protected set; }

        public ApplicationUsageItem(string identityName, string applicationName, string environmentName, DateTimeOffset? from, DateTimeOffset? to, long requests, long incomingTraffic, long outgoingTraffic)
        {
            IdentityName = identityName;
            ApplicationName = applicationName;
            EnvironmentName = environmentName;
            From = from;
            To = to;
            Requests = requests;
            IncomingTraffic = incomingTraffic;
            OutgoingTraffic = outgoingTraffic;
        }
    }
}
