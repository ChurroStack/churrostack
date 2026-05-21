using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetEmbeddingsWithLlmHandler : IRequestHandler<GetEmbeddingsWithLlm, ValueTask<JsonElement>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GetEmbeddingsWithLlmHandler> _logger;
        private readonly ITenantResolver _tenantResolver;
        private readonly IAppCache _appCache;
        private readonly RunnerService _runnerService;
        private readonly MetricsAggregatorService _metricsAggregatorService;
        private static Random _random = new Random();

        public GetEmbeddingsWithLlmHandler(
            ChurrosDbContext context,
            IMediator mediator,
            IHttpClientFactory httpClientFactory,
            ILogger<GetEmbeddingsWithLlmHandler> logger,
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

        public async ValueTask<JsonElement> Handle(GetEmbeddingsWithLlm request, CancellationToken cancellationToken)
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

            var destinationUriBuilder = new UriBuilder(destination.Uri);
            if (destinationUriBuilder.Scheme == "internal")
            {
                (string destinationUrl, HttpClient httpClient) internalAppProxy = await CompletePromptWithLlmHandler.GetInternalDestination(_appCache, _context, _runnerService, destinationUriBuilder);
                destinationUriBuilder = new UriBuilder(internalAppProxy.destinationUrl);
                httpClient = internalAppProxy.httpClient;
            }

            destinationUriBuilder.Path = $"{destinationUriBuilder.Path.TrimEnd('/')}/v1/embeddings";
            var message = new HttpRequestMessage(HttpMethod.Post, destinationUriBuilder.Uri);

            var requestBody = request.Body.Deserialize<JsonObject>()!;
            requestBody["model"] = destination.Model;

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
            using var response = await httpClient.SendAsync(message, cancellationToken);
            await response.HandleException();
            var content = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), JsonSettings.Value)!;
            var metrics = new List<MetricItem>();
            if (content.TryGetProperty("usage", out var usage))
            {
                if (usage.ValueKind == JsonValueKind.Object)
                {
                    CompletePromptWithLlmHandler.HandleMetrics(request?.XUserId, _logger, _tenantResolver, item, destination, metrics, destinationUriBuilder, usage);
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
            return content;
        }
    }
}
