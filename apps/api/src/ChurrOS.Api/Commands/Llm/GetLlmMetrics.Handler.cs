using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Commands.Metrics;
using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetLlmMetricsHandler : IRequestHandler<GetLlmMetrics, ValueTask<MetricValuesItem>>
    {
        private static readonly Dictionary<string, string[]> RateMetricMap = new(StringComparer.Ordinal)
        {
            { "requests_per_minute", new[] { "completion_count" } },
            { "tokens_per_minute", new[] { "prompt_tokens", "completion_tokens" } },
        };

        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;
        private readonly IMetricsBucketService _bucketService;

        public GetLlmMetricsHandler(IMediator mediator, ChurrosDbContext context, IMetricsBucketService bucketService)
        {
            _mediator = mediator;
            _context = context;
            _bucketService = bucketService;
        }

        public async ValueTask<MetricValuesItem> Handle(GetLlmMetrics request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Llm>();
            var item = await repo
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == request.LlmId);

            if (item == null)
            {
                throw new NotFoundException($"LLm with id '{request.LlmId}' was not found.");
            }

            if (!await _mediator.Send(new IsAdminOrHasAcl(item.AclId, Permission.Read), cancellationToken))
                throw new UnauthorizedAccessException();

            var responseLabels = new Dictionary<string, string> { { "metric", request.MetricName } };
            if (!string.IsNullOrWhiteSpace(request.IdentityName)) responseLabels["identity_name"] = request.IdentityName;
            if (!string.IsNullOrWhiteSpace(request.UserId)) responseLabels["x_user_id"] = request.UserId;
            if (!string.IsNullOrWhiteSpace(request.Model)) responseLabels["destination_model"] = request.Model;

            if (RateMetricMap.TryGetValue(request.MetricName, out var underlyingMetrics))
            {
                // Synthetic rate metric: fetch underlying stored metric series scoped to this LLM,
                // combine, and build a peak-per-minute series.
                var llmIdStr = item.Id.ToString();
                var combinedSeries = new List<MetricSeriesInfo>();
                foreach (var underlying in underlyingMetrics)
                {
                    var filter = new Dictionary<string, string>(responseLabels)
                    {
                        ["llm_id"] = llmIdStr,
                        ["metric"] = underlying
                    };
                    var series = await _context.Set<Metric>()
                        .Where(o => EF.Functions.JsonContains(o.Labels, filter))
                        .Select(o => new MetricSeriesInfo(o.MetricId, o.Type, o.Labels))
                        .ToListAsync(cancellationToken);
                    combinedSeries.AddRange(series);
                }

                return await _bucketService.BuildPeakPerMinuteSeriesAsync(
                    request.MetricName,
                    responseLabels,
                    combinedSeries,
                    request.From,
                    request.To,
                    request.Tz,
                    cancellationToken);
            }

            // Non-rate metric: delegate to GetMetrics unchanged (original path).
            var storedFilter = new Dictionary<string, string>
            {
                { "llm_id", item.Id.ToString() },
                { "metric", request.MetricName }
            };
            if (!string.IsNullOrWhiteSpace(request.IdentityName)) storedFilter["identity_name"] = request.IdentityName;
            if (!string.IsNullOrWhiteSpace(request.UserId)) storedFilter["x_user_id"] = request.UserId;
            if (!string.IsNullOrWhiteSpace(request.Model)) storedFilter["destination_model"] = request.Model;

            return await _mediator.Send(new GetMetrics(storedFilter, request.From, request.To, request.Tz), cancellationToken);
        }
    }
}
