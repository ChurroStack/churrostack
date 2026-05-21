using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Commands.Metrics;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Llm;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetLlmUsageHandler : IRequestHandler<GetLlmUsage, ValueTask<QueryResult<LlmUsageItem>>>
    {
        private record MetricValueEntry(long MetricId, DateTimeOffset Timestamp, double Value);
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;

        public GetLlmUsageHandler(IMediator mediator, ChurrosDbContext context)
        {
            _mediator = mediator;
            _context = context;
        }

        public async ValueTask<QueryResult<LlmUsageItem>> Handle(GetLlmUsage request, CancellationToken cancellationToken)
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


            if (!string.IsNullOrWhiteSpace(request.GroupBy) && request.GroupBy != "identity_name")
            {
                throw new ArgumentException("Invalid group by value. Only 'identity_name' is supported.");
            }

            IDictionary<string, double> completions = new Dictionary<string, double>();
            IDictionary<string, double> completionTokens = new Dictionary<string, double>();
            IDictionary<string, double> promptTokens = new Dictionary<string, double>();

            try
            {
                var filter = new Dictionary<string, string>
                {
                    { "llm_id", item.Id.ToString() },
                    { "metric", "completion_tokens" }
                };
                completionTokens = await _mediator.Send(new GetMetricTotals(filter, request.GroupBy ?? "identity_name", request.From, request.To), cancellationToken);
            }
            catch (Exception ex)
            {
                // Ignore metric retrieval errors and return empty usage
            }
            try
            {
                var filter = new Dictionary<string, string>
                {
                    { "llm_id", item.Id.ToString() },
                    { "metric", "prompt_tokens" }
                };
                promptTokens = await _mediator.Send(new GetMetricTotals(filter, request.GroupBy ?? "identity_name", request.From, request.To), cancellationToken);
            }
            catch (Exception ex)
            {
                // Ignore metric retrieval errors and return empty usage
            }
            try
            {
                var filter = new Dictionary<string, string>
                {
                    { "llm_id", item.Id.ToString() },
                    { "metric", "completion_count" }
                };
                completions = await _mediator.Send(new GetMetricTotals(filter, request.GroupBy ?? "identity_name", request.From, request.To), cancellationToken);
            }
            catch (Exception ex)
            {
                // Ignore metric retrieval errors and return empty usage
            }

            var result = new List<LlmUsageItem>();

            foreach (var groupName in completions.Keys.Union(completionTokens.Keys).Union(promptTokens.Keys).Distinct())
            {
                completions.TryGetValue(groupName, out double completionCount);
                completionTokens.TryGetValue(groupName, out double completionTokenCount);
                promptTokens.TryGetValue(groupName, out double promptTokenCount);
                var usageItem = new LlmUsageItem(groupName, (long)promptTokenCount, (long)completionTokenCount, (long)completionCount);
                result.Add(usageItem);
            }

            switch (request.OrderBy?.ToLower())
            {
                case "prompt_tokens":
                    result = request.OrderDirection.ToLower() == "desc"
                        ? result.OrderByDescending(o => o.PromptTokens).ToList()
                        : result.OrderBy(o => o.PromptTokens).ToList();
                    break;
                case "completion_tokens":
                    result = request.OrderDirection.ToLower() == "desc"
                        ? result.OrderByDescending(o => o.CompletionTokens).ToList()
                        : result.OrderBy(o => o.CompletionTokens).ToList();
                    break;
                default:
                case "completions":
                    result = request.OrderDirection.ToLower() == "desc"
                        ? result.OrderByDescending(o => o.Completions).ToList()
                        : result.OrderBy(o => o.Completions).ToList();
                    break;
            }

            return new QueryResult<LlmUsageItem>(result);
        }
    }
}
