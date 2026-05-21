using System.Text.Json;

namespace ChurrOS.Api.Models.Dtos.Application
{
    public class ApplicationTraceItem
    {
        public string? IdentityName { get; protected set; }
        public string ApplicationName { get; protected set; }
        public string? Protocol { get; protected set; }
        public string? Method { get; protected set; }
        public string? Service { get; protected set; }
        public string? Host { get; protected set; }
        public string? Path { get; protected set; }
        public int? StatusCode { get; protected set; }
        public bool IsError { get; protected set; }
        public string? ClientIp { get; protected set; }
        public long RequestBytes { get; protected set; }
        public long ResponseBytes { get; protected set; }
        public long Duration { get; protected set; }
        public JsonElement? Tags { get; protected set; }
        public DateTimeOffset Timestamp { get; protected set; }

        public ApplicationTraceItem(string? identityName, string applicationName, string? protocol, string? method, string? service, string? host, string? path, int? statusCode, bool isError, string? clientIp, long requestBytes, long responseBytes, long duration, JsonElement? tags, DateTimeOffset timestamp)
        {
            IdentityName = identityName;
            ApplicationName = applicationName;
            Protocol = protocol;
            Method = method;
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
            Timestamp = timestamp;
        }
    }
}
