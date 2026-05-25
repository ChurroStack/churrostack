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
    public class GetApplicationUsageHandler : IRequestHandler<GetApplicationUsage, ValueTask<QueryResult<ApplicationUsageItem>>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;
        private readonly IAppCache _appCache;

        public GetApplicationUsageHandler(ChurrosDbContext context, IMediator mediator, IAppCache appCache)
        {
            _context = context;
            _mediator = mediator;
            _appCache = appCache;
        }

        public async ValueTask<QueryResult<ApplicationUsageItem>> Handle(GetApplicationUsage request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Application>();

            var item = await repo
                .AsNoTracking()
                .Include(o => o.Environment)
                .Where(o => o.Name == request.AppName)
                .Select(o => new { o.Id, o.AclId, o.Name, EnvironmentAclId = o.Environment!.AclId, EnvironmentName = o.Environment.Name })
                .FirstOrDefaultAsync();

            if (item == null)
            {
                throw new NotFoundException($"Application with name '{request.AppName}' was not found.");
            }

            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Read), cancellationToken);
                if (!identityAcls.ContainsKey(item.AclId) && !identityAcls.ContainsKey(item.EnvironmentAclId))
                    throw new UnauthorizedAccessException("You do not have permission to read this application.");
            }

            // Set the time range window (UTC defaults so behavior is independent of the server's TZ).
            var from = request.Query.From ?? DateTime.UtcNow.Date;
            var to = request.Query.To ?? DateTime.UtcNow.Date.AddDays(1);

            if (from >= to)
            {
                throw new ArgumentException("The 'From' date must be earlier than the 'To' date.");
            }

            var baseQuery = _context.Set<Domain.ApplicationTrace>()
                .AsNoTracking()
                .Where(o => o.ApplicationId == item.Id && from <= o.Timestamp && o.Timestamp <= to);

            switch (request.Query.GroupBy?.ToLowerInvariant())
            {
                case "identity":
                    var query = baseQuery
                        .GroupBy(o => o.IdentityId)
                        .Select(g => new
                        {
                            IdentityId = g.Key,
                            From = g.Min(o => (DateTimeOffset?)o.Timestamp),
                            To = g.Max(o => (DateTimeOffset?)o.Timestamp),
                            Requests = g.Count(),
                            IncomingTraffic = g.Sum(x => (long?)x.RequestBytes) ?? 0,
                            OutgoingTraffic = g.Sum(x => (long?)x.ResponseBytes) ?? 0
                        });
                    switch (request.Query.OrderBy?.ToLowerInvariant())
                    {
                        case null:
                        case "":
                        case "requests":
                            query = query.OrderByDescending(o => o.Requests);
                            break;
                        case "incomingTraffic":
                            query = query.OrderByDescending(o => o.IncomingTraffic);
                            break;
                        case "outgoingTraffic":
                            query = query.OrderByDescending(o => o.OutgoingTraffic);
                            break;
                        default:
                            throw new ArgumentException("The 'OrderBy' parameter is invalid. Supported values: 'requests', 'requestBytes' and 'responseBytes.");
                    }

                    var count = await query.CountAsync(cancellationToken);

                    query = request.Query?.ApplyPaginationTo(query) ?? query;

                    var items = await query.ToListAsync();

                    var result = new List<ApplicationUsageItem>();
                    foreach (var entry in items)
                    {
                        result.Add(new ApplicationUsageItem(await GetIdentityName(entry.IdentityId), item.Name, item.EnvironmentName, entry.From, entry.To, entry.Requests, entry.IncomingTraffic, entry.OutgoingTraffic));
                    }
                    return new QueryResult<ApplicationUsageItem>(result, count);
                default:
                    throw new ArgumentException("The 'GroupBy' parameter is invalid. Supported values: 'identity'.");
            }
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
