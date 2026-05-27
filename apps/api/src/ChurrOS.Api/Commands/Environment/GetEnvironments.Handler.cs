using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Environment;
using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR;
using DispatchR.Abstractions.Send;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Environment
{
    public class GetEnvironmentsHandler : IRequestHandler<GetEnvironments, ValueTask<QueryResult<EnvironmentSummary>>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;

        public GetEnvironmentsHandler(ChurrosDbContext context, IMapper mapper, IMediator mediator)
        {
            _context = context;
            _mapper = mapper;
            _mediator = mediator;
        }

        public async ValueTask<QueryResult<EnvironmentSummary>> Handle(GetEnvironments request, CancellationToken cancellationToken)
        {
            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            long[]? aclIds = null;
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Read), cancellationToken);
                aclIds = identityAcls.Keys.ToArray();
            }

            IQueryable<Domain.Environment> query = _context.Set<Domain.Environment>()
                .AsNoTracking()
                .Include(o => o.CreatedBy)
                .Include(o => o.ModifiedBy);

            if (!isAdmin)
                query = query.Where(o => aclIds!.Contains(o.AclId));

            if (!string.IsNullOrWhiteSpace(request.Query?.Search))
            {
                query = query.Where(o => o.Name.Contains(request.Query.Search));
            }

            if (request.Query?.Tags is { Length: > 0 } tags)
            {
                // Npgsql 8+ translates this to `tags <@ o.Tags` (== `o.Tags @> tags`), GIN-indexable.
                var tagsCount = tags.Length;
                query = query.Where(o => o.Tags.Length >= tagsCount && tags.All(t => o.Tags.Contains(t)));
            }

            var count = await query.CountAsync(cancellationToken);

            query = request.Query?.ApplyPaginationTo(query) ?? query;

            var items = await _mapper
                .From(query)
                .ProjectToType<EnvironmentSummary>()
                .ToListAsync();

            return new QueryResult<EnvironmentSummary>(items, count);
        }
    }
}
