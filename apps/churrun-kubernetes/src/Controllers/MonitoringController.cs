using ChurrunKubernetes.Models.Logs;
using ChurrunKubernetes.Services;
using ChurrunKubernetes.Services.State;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace ChurrunKubernetes.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/monitoring")]
    public class MonitoringController : ControllerBase
    {
        private static JsonSerializerOptions? _jsonSerializerOptions;
        private readonly EventsStateService<KubernetesDeploymentEvent> _stateChangesService;
        private readonly EventsStateService<KubernetesGenericEvent> _eventsStateService;
        private readonly KubernetesService _kubernetesService;
        private readonly IConfiguration _configuration;

        public MonitoringController(
            EventsStateService<KubernetesDeploymentEvent> stateChangesService,
            EventsStateService<KubernetesGenericEvent> eventsStateService,
            KubernetesService kubernetesService,
            IConfiguration configuration)
        {
            _stateChangesService = stateChangesService;
            _eventsStateService = eventsStateService;
            _kubernetesService = kubernetesService;
            _configuration = configuration;
        }

        [HttpGet("events")]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task StreamEvents(CancellationToken cancellationToken = default)
        {
            Response.Headers.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            while (!cancellationToken.IsCancellationRequested)
            {
                await foreach (var @event in _eventsStateService.GetEventsAsync(cancellationToken))
                {
                    await SendUpdate(@event, cancellationToken);
                }
                await SendKeepalive(cancellationToken);
            }

            await SendUpdate<object>(null, cancellationToken);
        }

        [HttpGet("state")]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task StreamChanges(CancellationToken cancellationToken = default)
        {
            Response.Headers.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            while (!cancellationToken.IsCancellationRequested)
            {
                await foreach (var @event in _stateChangesService.GetEventsAsync(cancellationToken))
                {
                    await SendUpdate(@event, cancellationToken);
                }
                await SendKeepalive(cancellationToken);
            }

            await SendUpdate<object>(null, cancellationToken);
        }

        [HttpGet("metrics")]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(KubernetesMetric[]), StatusCodes.Status200OK)]
        public async Task<IActionResult> Metrics(CancellationToken cancellationToken = default)
        {
            var response = await _kubernetesService.ScrapeMetricsAsync(_configuration["Kubernetes:Namespace"]!, cancellationToken);
            return Ok(response);
        }

        [HttpGet("console/{appName}")]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task WatchLogsAsync(string appName, CancellationToken cancellationToken)
        {
            Response.Headers.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            var @namespace = _configuration["Kubernetes:Namespace"]!;
            var podNames = await _kubernetesService.GetDeploymentPodsAsync(@namespace, appName, cancellationToken);
            var containerNames = podNames.Select(podName => _kubernetesService.GetPodContainersAsync(@namespace, podName, cancellationToken).GetAwaiter().GetResult().Select(containerName => (podName, containerName)))
               .SelectMany(t => t)
               .ToArray();

            if (containerNames.Length == 0)
            {
                return;
            }

            var buffer = new BufferBlock<string>();
            var tasks = new List<Task>();
            foreach (var item in containerNames)
            {
                var (podName, containerName) = item;
                tasks.Add(Task.Run(async () =>
                {
                    await foreach (var item in _kubernetesService.MonitorLogsAsync(@namespace, podName, containerName, cancellationToken))
                    {
                        buffer.Post($"[{podName}/{containerName}] {item}");
                    }
                }));
            }
            var consumer = Task.Run(async () =>
            {
                while (await buffer.OutputAvailableAsync(cancellationToken))
                {
                    var logLine = await buffer.ReceiveAsync(cancellationToken);
                    await SendUpdate(logLine, cancellationToken);
                }

                await SendUpdate<object>(null, cancellationToken);
            });

            await Task.WhenAll(tasks);
            buffer.Complete();
        }

        private async Task SendUpdate<T>(T? @event, CancellationToken cancellationToken)
        {
            if (_jsonSerializerOptions is null)
            {
                _jsonSerializerOptions = new JsonSerializerOptions(JsonSettings.Value)
                {
                    WriteIndented = false
                };

                _jsonSerializerOptions.ApplyDefaultOptions();
            }

            if (@event is not null)
            {
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes($"data: {JsonSerializer.Serialize(@event, _jsonSerializerOptions).Trim('\r', '\n')}\r\n\r\n"), cancellationToken);
                //await Response.Body.FlushAsync(cancellationToken);
            }
        }

        private async Task SendKeepalive(CancellationToken cancellationToken)
        {
            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes($": heartbeat\n\n"), cancellationToken);
        }
    }
}
