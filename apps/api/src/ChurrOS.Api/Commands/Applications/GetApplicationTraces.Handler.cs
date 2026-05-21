using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetApplicationTracesHandler : IRequestHandler<GetApplicationTraces, ValueTask<QueryResult<ApplicationTraceItem>>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;
        private readonly IAppCache _appCache;

        public GetApplicationTracesHandler(ChurrosDbContext context, IMediator mediator, IAppCache appCache)
        {
            _context = context;
            _mediator = mediator;
            _appCache = appCache;
        }

        public async ValueTask<QueryResult<ApplicationTraceItem>> Handle(GetApplicationTraces request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Application>();

            var app = await repo
                .AsNoTracking()
                .Include(o => o.Environment)
                .Where(o => o.Name == request.AppName)
                .Select(o => new { o.Id, o.Name, o.AclId, EnvironmentAclId = o.Environment!.AclId })
                .FirstOrDefaultAsync();

            if (app == null)
            {
                throw new NotFoundException($"Application with name '{request.AppName}' was not found.");
            }

            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Read), cancellationToken);
                if (!identityAcls.ContainsKey(app.AclId) && !identityAcls.ContainsKey(app.EnvironmentAclId))
                    throw new UnauthorizedAccessException("You do not have permission to read this application.");
            }

            // Set the time range window
            var from = request.Query.From ?? DateTime.Today;
            var to = request.Query.To ?? DateTime.Today.AddDays(1);

            if (from >= to)
            {
                throw new ArgumentException("The 'From' date must be earlier than the 'To' date.");
            }

            var query = _context.Set<Domain.ApplicationTrace>()
                .AsNoTracking()
                .Where(o => o.ApplicationId == app.Id && from <= o.Timestamp && o.Timestamp <= to);

            if (!string.IsNullOrWhiteSpace(request.Query.IdentityName))
            {
                var identityId = await _mediator.Send(new GetIdentityId(request.Query.IdentityName), cancellationToken);
                query = query.Where(o => o.IdentityId == identityId);
            }

            var count = await query.CountAsync(cancellationToken);

            query = request.Query?.ApplyPaginationTo(query) ?? query;

            query.OrderByDescending(o => o.Timestamp);

            var items = await query.ToListAsync();

            var result = new List<ApplicationTraceItem>();
            await foreach (var trace in items.ToAsyncEnumerable())
            {
                var identityName = trace.IdentityId.HasValue ? await GetIdentityName(trace.IdentityId) : "";
                result.Add(new ApplicationTraceItem(
                    identityName: identityName,
                    applicationName: app.Name,
                    protocol: trace.Protocol,
                    method: trace.Method,
                    service: trace.Service,
                    host: trace.Host,
                    path: trace.Path,
                    statusCode: trace.StatusCode,
                    isError: trace.IsError,
                    clientIp: trace.ClientIp,
                    requestBytes: trace.RequestBytes,
                    responseBytes: trace.ResponseBytes,
                    duration: trace.Duration,
                    tags: trace.Tags,
                    timestamp: trace.Timestamp)
                );
            }
            return new QueryResult<ApplicationTraceItem>(result, count);
        }

        private async Task<string> GetIdentityName(long? identityId)
        {
            if (!identityId.HasValue)
                return "";

            return await _appCache.GetOrAddAsync($"identity:{identityId.Value}:name", async (ctx) =>
            {
                ctx.SetAbsoluteExpiration(TimeSpan.FromHours(1));
                var identity = await _context.Set<Domain.Identity>()
                    .AsNoTracking()
                    .Where(o => o.Id == identityId.Value)
                    .Select(o => o.Name)
                    .FirstOrDefaultAsync();
                return identity ?? "";
            });
        }
    }
}
