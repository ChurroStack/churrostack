using ChurrOS.Api.Utils;
using System.Text.Json;

namespace ChurrOS.Api.Domain
{
    public class ApplicationTrace
    {
        [HypertablePartitionColumn]
        public long AccountId { get; set; }

        public long EnvironmentId { get; set; }
        public long ApplicationId { get; set; }

        [HypertableColumn]
        public DateTimeOffset Timestamp { get; set; }

        public long? IdentityId { get; set; }

        /// <summary>
        /// GET, POST (nullable for tcp)
        /// </summary>
        public string? Method { get; set; }

        /// <summary>
        /// http1, http2, grpc, tcp
        /// </summary>
        public string? Protocol { get; set; }
        public string? Service { get; set; }
        public string? Host { get; set; }
        public string? Path { get; set; }
        public int? StatusCode { get; set; }
        public bool IsError { get; set; }
        public string? ClientIp { get; set; }
        public long RequestBytes { get; set; }
        public long ResponseBytes { get; set; }
        public long Duration { get; set; }
        public JsonElement? Tags { get; set; }

        public ApplicationTrace(long accountId, long applicationId, long environmentId, DateTimeOffset timestamp, long? identityId, string? method, string? protocol, string? service, string? host, string? path, int? statusCode, bool isError, string? clientIp, long requestBytes, long responseBytes, long duration, JsonElement? tags)
        {
            AccountId = accountId;
            ApplicationId = applicationId;
            EnvironmentId = environmentId;
            Timestamp = timestamp;
            IdentityId = identityId;
            Method = method;
            Protocol = protocol;
            Service = service;
            Host = host;
            Path = path;
            StatusCode = statusCode;
            IsError = isError;
            ClientIp = clientIp;
            RequestBytes = requestBytes;
            ResponseBytes = responseBytes;
            Duration = duration;
            Tags = tags;
        }
    }
}
