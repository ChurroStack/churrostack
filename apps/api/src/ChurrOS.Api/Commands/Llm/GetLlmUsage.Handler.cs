using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Commands.Metrics;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Llm;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetLlmUsageHandler : IRequestHandler<GetLlmUsage, ValueTask<QueryResult<LlmUsageItem>>>
    {
        private static readonly HashSet<string> AllowedGroupBys = new(StringComparer.Ordinal)
        {
            "identity_name", "destination_model", "x_user_id"
        };

        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;
        private readonly ILogger<GetLlmUsageHandler> _logger;

        public GetLlmUsageHandler(IMediator mediator, ChurrosDbContext context, ILogger<GetLlmUsageHandler> logger)
        {
            _mediator = mediator;
            _context = context;
            _logger = logger;
        }

        public async ValueTask<QueryResult<LlmUsageItem>> Handle(GetLlmUsage request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Llm>();
            var item = await repo
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == request.LlmId, cancellationToken);

            if (item == null)
            {
                throw new NotFoundException($"LLm with id '{request.LlmId}' was not found.");
            }

            if (!await _mediator.Send(new IsAdminOrHasAcl(item.AclId, Permission.Read), cancellationToken))
                throw new UnauthorizedAccessException();

            var groupBy = string.IsNullOrWhiteSpace(request.GroupBy) ? "identity_name" : request.GroupBy;
            if (!AllowedGroupBys.Contains(groupBy))
            {
                throw new ArgumentException($"Invalid groupBy '{groupBy}'. Allowed: identity_name, destination_model, x_user_id.");
            }

            _logger.LogInformation(
                "llm.usage llmId={LlmId} groupBy={GroupBy} from={From} to={To} hasIdentityFilter={HasIdentity} hasUserIdFilter={HasUserId} hasModelFilter={HasModel}",
                request.LlmId,
                groupBy,
                request.From,
                request.To,
                !string.IsNullOrWhiteSpace(request.IdentityName),
                !string.IsNullOrWhiteSpace(request.UserId),
                !string.IsNullOrWhiteSpace(request.Model));

            var (priceByHostModel, priceByModel) = LlmUsageAggregator.BuildPriceMaps(
                item.Destination ?? Array.Empty<LLmDestinationItem>());

            var baseLabels = new Dictionary<string, string>
            {
                { "llm_id", item.Id.ToString() }
            };
            AddIfPresent(baseLabels, "identity_name", request.IdentityName);
            AddIfPresent(baseLabels, "x_user_id", request.UserId);
            AddIfPresent(baseLabels, "destination_model", request.Model);

            var promptSeries = await FetchSeriesSafeAsync("prompt_tokens", baseLabels, request, cancellationToken);
            var completionSeries = await FetchSeriesSafeAsync("completion_tokens", baseLabels, request, cancellationToken);
            var countSeries = await FetchSeriesSafeAsync("completion_count", baseLabels, request, cancellationToken);

            var rows = LlmUsageAggregator.BuildRows(
                groupBy,
                promptSeries,
                completionSeries,
                countSeries,
                priceByHostModel,
                priceByModel);
            rows = LlmUsageAggregator.Sort(rows, request.OrderBy, request.OrderDirection);

            _logger.LogInformation("llm.usage llmId={LlmId} groupBy={GroupBy} rows={RowCount}", request.LlmId, groupBy, rows.Count);

            return new QueryResult<LlmUsageItem>(rows);
        }

        private async Task<List<MetricSeriesTotal>> FetchSeriesSafeAsync(string metricName, IDictionary<string, string> baseLabels, GetLlmUsage request, CancellationToken cancellationToken)
        {
            var labels = new Dictionary<string, string>(baseLabels) { { "metric", metricName } };
            try
            {
                return await _mediator.Send(new GetMetricSeriesTotals(labels, request.From, request.To), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "llm.usage no series for metric={Metric} llmId={LlmId}", metricName, request.LlmId);
                return new List<MetricSeriesTotal>();
            }
        }

        private static void AddIfPresent(IDictionary<string, string> labels, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                labels[key] = value;
            }
        }
    }
}
