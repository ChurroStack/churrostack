using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Services;
using System.Text.Json;
using Yarp.ReverseProxy.Forwarder;

namespace ChurrOS.Api.Middlewares
{
    /// <summary>
    ///  Middleware that collects YARP metrics and logs them at the end of each request
    /// </summary>
    public class ProxyTraceMiddleware
    {
        public class PerRequestMetrics
        {
            private static readonly AsyncLocal<PerRequestMetrics> _local = new AsyncLocal<PerRequestMetrics>();
            private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

            // Ensure we are only fetched via the factory
            private PerRequestMetrics() { }

            /// <summary>
            /// Factory to instantiate or restore the metrics from AsyncLocal storage
            /// </summary>
            public static PerRequestMetrics Current => _local.Value ??= new PerRequestMetrics();

            // Time the request was started via the pipeline
            public DateTime StartTime { get; set; }

            // Offset Tics for each part of the proxy operation
            public float RouteInvokeOffset { get; set; }
            public float ProxyStartOffset { get; set; }
            public float HttpRequestStartOffset { get; set; }
            public float HttpConnectionEstablishedOffset { get; set; }
            public float HttpRequestLeftQueueOffset { get; set; }

            public float HttpRequestHeadersStartOffset { get; set; }
            public float HttpRequestHeadersStopOffset { get; set; }
            public float HttpRequestContentStartOffset { get; set; }
            public float HttpRequestContentStopOffset { get; set; }

            public float HttpResponseHeadersStartOffset { get; set; }
            public float HttpResponseHeadersStopOffset { get; set; }
            public float HttpResponseContentStopOffset { get; set; }

            public float HttpRequestStopOffset { get; set; }
            public float ProxyStopOffset { get; set; }

            // Info about the request
            public ForwarderError Error { get; set; }
            public long RequestBodyLength { get; set; }
            public long ResponseBodyLength { get; set; }
            public long RequestContentIops { get; set; }
            public long ResponseContentIops { get; set; }
            public string DestinationId { get; set; }
            public string ClusterId { get; set; }
            public string RouteId { get; set; }

            public string ToJson()
            {
                return JsonSerializer.Serialize(this, _jsonOptions);
            }

            public float CalcOffset(DateTime timestamp)
            {
                return (float)(timestamp - StartTime).TotalMilliseconds;
            }
        }

        // Required for middleware
        private readonly RequestDelegate _next;
        // Supplied via DI
        private readonly ILogger<ProxyTraceMiddleware> _logger;
        private readonly IQueueService _queueService;

        public ProxyTraceMiddleware(RequestDelegate next, ILogger<ProxyTraceMiddleware> logger, IQueueService queueService)
        {
            _logger = logger;
            _next = next;
            _queueService = queueService;
        }

        /// <summary>
        /// Entrypoint for being called as part of the request pipeline
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            var metrics = PerRequestMetrics.Current;
            metrics.StartTime = DateTime.UtcNow;

            // Call the next steps in the middleware, including the proxy
            await _next(context);

            if (context.Request.Path.StartsWithSegments("/share"))
            {
                try
                {
                    var cancellationToken = context.RequestAborted;
                    var request = context.Request;
                    var pathParts = request.Path.Value?.Split("/", StringSplitOptions.RemoveEmptyEntries);
                    if (pathParts == null || pathParts.Length < 2)
                    {
                        _logger.LogWarning("ProxyTraceMiddleware: Unable to parse application name from path {Path}", request.Path.Value);
                        return;
                    }
                    var path = string.Join('/', pathParts.Skip(3));
                    if (string.IsNullOrWhiteSpace(path))
                        path = "/";
                    if (!path.StartsWith('/'))
                    {
                        path = $"/{path}";
                    }
                    if (request.Path.Value?.EndsWith('/') ?? false)
                    {
                        path = $"{path}/";
                    }

                    await _queueService.ProduceAsync("traces-queue", new ApplicationTraceItem(
                        identityName: context.User?.Identity?.Name,
                        applicationName: pathParts[1],
                        protocol: request.Protocol,
                        method: request.Method,
                        service: pathParts[2],
                        host: request.Host.Value,
                        path: path,
                        statusCode: (int)context.Response.StatusCode,
                        isError: context.Response.StatusCode >= 400,
                        clientIp: context.Connection?.RemoteIpAddress?.ToString(),
                        requestBytes: (long)(double)metrics.HttpRequestStopOffset,
                        responseBytes: (long)(double)(metrics.HttpResponseContentStopOffset == 0 ? metrics.HttpResponseHeadersStopOffset : metrics.HttpResponseContentStopOffset),
                        duration: (long)metrics.CalcOffset(DateTime.Now),
                        null,
                        DateTimeOffset.Now
                    ), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to log YARP metrics");
                }
            }
        }
    }

    /// <summary>
    /// Helper to aid with registration of the middleware
    /// </summary>
    public static class YarpMetricCollectionMiddlewareHelper
    {
        public static IApplicationBuilder UsePerRequestMetricCollection(
          this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ProxyTraceMiddleware>();
        }
    }
}
