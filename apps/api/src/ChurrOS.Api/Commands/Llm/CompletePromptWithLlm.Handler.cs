using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Llm;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Security;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Stream;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks.Dataflow;

namespace ChurrOS.Api.Commands.Llm
{
    public class CompletePromptWithLlmHandler : IStreamRequestHandler<CompletePromptWithLlm, JsonElement>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CompletePromptWithLlmHandler> _logger;
        private readonly ITenantResolver _tenantResolver;
        private readonly IAppCache _appCache;
        private readonly RunnerService _runnerService;
        private readonly MetricsAggregatorService _metricsAggregatorService;

        private static Random _random = new Random();

        public CompletePromptWithLlmHandler(
            ChurrosDbContext context,
            IMediator mediator,
            IHttpClientFactory httpClientFactory,
            ILogger<CompletePromptWithLlmHandler> logger,
            ITenantResolver tenantResolver,
            IAppCache appCache,
            RunnerService runnerService,
            MetricsAggregatorService metricsAggregatorService)
        {
            _context = context;
            _mediator = mediator;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _tenantResolver = tenantResolver;
            _appCache = appCache;
            _runnerService = runnerService;
            _metricsAggregatorService = metricsAggregatorService;
        }

        public async IAsyncEnumerable<JsonElement> Handle(CompletePromptWithLlm request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var model = request.Body.GetProperty("model").GetString();
            ArgumentException.ThrowIfNullOrEmpty(model, "Model is required");

            var item = await _context.Set<Domain.Llm>()
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Names.Any(x => x.Equals(model)));

            if (item == null)
            {
                throw new NotFoundException($"Model '{model}' was not found.");
            }

            var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Execute), cancellationToken);

            if (!identityAcls.ContainsKey(item.AclId))
            {
                throw new UnauthorizedAccessException("You do not have permission to use this model.");
            }

            if (item.Destination == null || item.Destination.Length == 0)
            {
                throw new NotFoundException($"No destinations configured for model '{model}'.");
            }

            var httpClient = _httpClientFactory.CreateClient();
            var destination = item.Destination[_random.Next(item.Destination.Length)];

            Exception? exception = null;
            var result = new BufferBlock<JsonElement>();
            var task = Task.Run(async () =>
            {
                var metrics = new List<MetricItem>();
                while (destination is not null && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var destinationUriBuilder = new UriBuilder(destination.Uri);
                        if (destinationUriBuilder.Scheme == "internal")
                        {
                            (string destinationUrl, HttpClient httpClient) internalAppProxy = await GetInternalDestination(_appCache, _context, _runnerService, destinationUriBuilder);
                            destinationUriBuilder = new UriBuilder(internalAppProxy.destinationUrl);
                            httpClient = internalAppProxy.httpClient;
                        }

                        destinationUriBuilder.Path = $"{destinationUriBuilder.Path.TrimEnd('/')}/chat/completions";
                        var message = new HttpRequestMessage(HttpMethod.Post, destinationUriBuilder.Uri);

                        var requestBody = request.Body.Deserialize<JsonObject>()!;
                        requestBody["model"] = destination.Model;

                        if (!string.IsNullOrWhiteSpace(destination.Patch))
                        {
                            var patchDoc = JsonSerializer.Deserialize<JsonElement>(destination.Patch);
                            if (patchDoc.ValueKind == JsonValueKind.Object)
                            {
                                requestBody.MergeWith(patchDoc);
                            }
                        }

                        if (request.IsStream)
                        {
                            if (requestBody.ContainsKey("stream_options"))
                            {
                                requestBody["stream_options"]!["include_usage"] = true;
                            }
                            else
                            {
                                requestBody["stream_options"] = JsonSerializer.SerializeToNode(new { include_usage = true }, JsonSettings.Value);
                            }
                        }

                        message.Content = new StringContent(requestBody.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
                        if (!string.IsNullOrWhiteSpace(destination.ApiKey))
                        {
                            message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", destination.ApiKey);
                        }
                        if (destination.Headers != null)
                        {
                            foreach (var header in destination.Headers)
                            {
                                message.Headers.TryAddWithoutValidation(header.Key, header.Value);
                            }
                        }

                        if (request.IsStream)
                        {
                            using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                            await response.HandleException();
                            await foreach (var @event in SseParser.Create(await response.Content.ReadAsStreamAsync(cancellationToken)).EnumerateAsync(cancellationToken))
                            {
                                if (@event.Data.Equals("[DONE]", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    break;
                                }
                                var content = JsonSerializer.Deserialize<JsonElement>(@event.Data, JsonSettings.Value)!;
                                if (content.TryGetProperty("usage", out var usage))
                                {
                                    if (usage.ValueKind == JsonValueKind.Object)
                                    {
                                        HandleMetrics(request?.XUserId, _logger, _tenantResolver, item, destination, metrics, destinationUriBuilder, usage);
                                    }
                                }
                                result.Post(content);
                            }
                        }
                        else
                        {
                            using var response = await httpClient.SendAsync(message, cancellationToken);
                            await response.HandleException();
                            var content = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), JsonSettings.Value)!;
                            if (content.TryGetProperty("usage", out var usage))
                            {
                                if (usage.ValueKind == JsonValueKind.Object)
                                {
                                    HandleMetrics(request?.XUserId, _logger, _tenantResolver, item, destination, metrics, destinationUriBuilder, usage);
                                }
                            }
                            result.Post(content);
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (item.Fallback is null)
                        {
                            exception = ex;
                            break;
                        }
                        if (item.Fallback == destination)
                        {
                            exception = ex;
                            break;
                        }
                        destination = item.Fallback;
                    }
                }
                try
                {
                    if (metrics.Count > 0)
                    {
                        foreach (var metric in metrics)
                        {
                            _metricsAggregatorService.AddMetric(metric);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log and ignore metric exceptions
                    _logger.LogError(ex, "Failed to enqueue LLM metrics");
                }
                result.Complete();
            }, cancellationToken);

            while (await result.OutputAvailableAsync(cancellationToken))
            {
                yield return await result.ReceiveAsync(cancellationToken);
            }
            if (exception is not null)
            {
                throw exception;
            }
        }

        internal static async Task<(string destinationUrl, HttpClient httpClient)> GetInternalDestination(IAppCache appCache, ChurrosDbContext context, RunnerService runnerService, UriBuilder destinationUriBuilder)
        {
            return await appCache.GetOrAddAsync($"internal-llm-url-{destinationUriBuilder.Host}", async ctx =>
            {
                ctx.SetAbsoluteExpiration(TimeSpan.FromMinutes(1));

                var query = (IQueryable<Domain.Application>)context.Set<Domain.Application>()
                    .AsNoTracking()
                    .Include(x => x.Environment);
                if (long.TryParse(destinationUriBuilder.Host, out var hostAppId))
                {
                    query = query.Where(x => x.Id == hostAppId);
                }
                else
                {
                    query = query.Where(x => x.Name == destinationUriBuilder.Host);
                }
                var app = await query
                    .Select(x => new { x.Name, EnvironmentName = x.Environment!.Name, EnvironmentHost = x.Environment.Host, EnvironmentEncryptionKey = x.Environment.EncryptionKey, EnvironmentId = x.Environment.Id, EnvironmentPort = x.Environment.Port, x.Ports })
                    .FirstOrDefaultAsync();

                if (app == null)
                    throw new ArgumentException($"Invalid application '{destinationUriBuilder.Host}'");
                var port = app.Ports?.FirstOrDefault(o => o.Protocol == Models.Dtos.Template.Definition.ProtocolType.OpenAI);
                if (port is null)
                {
                    throw new ArgumentException($"The application doesnt publish an OpenAI compatible port");
                }

                var ecParts = app.EnvironmentEncryptionKey.Split(':');
                var encryptionKey = AesGcmEncryption.Decrypt(ecParts[0], context.AccountEncryptionKey, ecParts[1]);
                var httpClient = runnerService.CreateHttpClient(app.EnvironmentHost[1], app.EnvironmentName, app.EnvironmentPort, encryptionKey);

                var parts = new List<string>
                    {
                        app.EnvironmentHost[1].TrimEnd('/'),
                        $"share/{app.Name}/{port.Name}"
                    };
                if (!string.IsNullOrEmpty(destinationUriBuilder.Path) && destinationUriBuilder.Path != "/")
                {
                    parts.Add(destinationUriBuilder.Path.Trim('/'));
                }

                var destinationUrl = string.Join('/', parts);
                return (destinationUrl, httpClient);
            });
        }

        internal static void HandleMetrics(string? xUserId, ILogger logger, ITenantResolver tenantResolver, Domain.Llm item, LLmDestinationItem destination, List<MetricItem> metrics, UriBuilder destinationUriBuilder, JsonElement usage)
        {
            try
            {
                var now = DateTimeOffset.Now;
                var labels = new Dictionary<string, string>()
                {
                    { "destination_model", destination.Model },
                    { "destination_host", destinationUriBuilder.Host }
                };
                if (!string.IsNullOrWhiteSpace(xUserId))
                {
                    labels.Add("x_user_id", xUserId);
                }
                if (!string.IsNullOrWhiteSpace(tenantResolver.Identity!.Name!))
                {
                    labels.Add("identity_name", tenantResolver.Identity.Name);
                }

                usage.TryGetProperty("prompt_tokens", out var jsonPromptTokens);
                if (int.TryParse(jsonPromptTokens.GetRawText(), out var promptTokens))
                {
                    metrics.Add(new MetricItem(item.AccountId, AddMetricName(labels, "prompt_tokens", item.Id), now, promptTokens, MetricType.Counter));
                }
                usage.TryGetProperty("completion_tokens", out var jsonCompletioTokens);
                if (int.TryParse(jsonCompletioTokens.GetRawText(), out var completionTokens))
                {
                    metrics.Add(new MetricItem(item.AccountId, AddMetricName(labels, "completion_tokens", item.Id), now, completionTokens, MetricType.Counter));
                }
                metrics.Add(new MetricItem(item.AccountId, AddMetricName(labels, "completion_count", item.Id), now, 1, MetricType.Counter));
            }
            catch (Exception ex)
            {
                // Log and ignore metric exceptions
                logger.LogError(ex, "Failed to enqueue LLM metrics");
            }
        }

        private static IDictionary<string, string> AddMetricName(Dictionary<string, string> tags, string metricName, long llmId)
        {
            var newDict = new Dictionary<string, string>(tags)
            {
                { "metric", metricName },
                { "llm_id", llmId.ToString() }
            };
            if (tags is not null)
            {
                foreach (var kvp in tags)
                {
                    newDict.TryAdd(kvp.Key, kvp.Value);
                }
            }
            return newDict;
        }
    }
}
