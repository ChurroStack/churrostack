using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Commands.Metrics;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetLlmMetricsHandler : IRequestHandler<GetLlmMetrics, ValueTask<MetricValuesItem>>
    {
        private record MetricValueEntry(long MetricId, DateTimeOffset Timestamp, double Value);
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;

        public GetLlmMetricsHandler(IMediator mediator, ChurrosDbContext context)
        {
            _mediator = mediator;
            _context = context;
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

            var filter = new Dictionary<string, string>
            {
                { "llm_id", item.Id.ToString() },
                { "metric", request.MetricName }
            };
            if (!string.IsNullOrWhiteSpace(request.IdentityName)) filter["identity_name"] = request.IdentityName;
            if (!string.IsNullOrWhiteSpace(request.UserId)) filter["x_user_id"] = request.UserId;
            if (!string.IsNullOrWhiteSpace(request.Model)) filter["destination_model"] = request.Model;

            return await _mediator.Send(new GetMetrics(filter, request.From, request.To, request.Tz), cancellationToken);
        }
    }
}
