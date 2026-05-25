using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Commands.Metrics;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetApplicationMetricsHandler : IRequestHandler<GetApplicationMetrics, ValueTask<MetricValuesItem>>
    {
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;

        public GetApplicationMetricsHandler(IMediator mediator, ChurrosDbContext context)
        {
            _mediator = mediator;
            _context = context;
        }

        public async ValueTask<MetricValuesItem> Handle(GetApplicationMetrics request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Application>();

            var app = await repo
                .Include(o => o.Environment)
                .Select(o => new { o.Id, o.Name, o.AclId, o.Environment })
                .FirstOrDefaultAsync(o => o.Name == request.AppName);

            if (app == null)
            {
                throw new NotFoundException($"Application with name '{request.AppName}' was not found.");
            }

            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Write), cancellationToken);
                if (!identityAcls.ContainsKey(app.AclId) && !identityAcls.ContainsKey(app.Environment!.AclId))
                    throw new UnauthorizedAccessException("You do not have permission to obtain application metrics.");
            }

            var filter = new Dictionary<string, string>
            {
                { "application_id", app.Id.ToString() },
                { "metric", request.MetricName }
            };

            return await _mediator.Send(new GetMetrics(filter, request.From, request.To, request.Tz), cancellationToken);
        }
    }
}
