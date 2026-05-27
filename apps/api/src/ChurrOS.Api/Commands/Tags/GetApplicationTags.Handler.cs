using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Tags
{
    public class GetApplicationTagsHandler : IRequestHandler<GetApplicationTags, ValueTask<string[]>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;

        public GetApplicationTagsHandler(ChurrosDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async ValueTask<string[]> Handle(GetApplicationTags request, CancellationToken cancellationToken)
        {
            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);

            IQueryable<Domain.Application> query = _context.Set<Domain.Application>().AsNoTracking();

            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, request.Permission), cancellationToken);
                var aclIds = identityAcls.Keys.ToArray();
                var envIds = await _context.Set<Domain.Environment>()
                    .AsNoTracking()
                    .Where(o => aclIds.Contains(o.AclId))
                    .Select(o => o.Id)
                    .ToListAsync(cancellationToken);
                query = query.Where(o => aclIds.Contains(o.AclId) || envIds.Contains(o.EnvironmentId));
            }

            // Cap at 500 distinct tags to bound payload + DB scan when no GIN-eligible index exists
            // for this distinct projection. If tenants outgrow this, introduce a Redis cache keyed
            // by (accountId, hashOfAclIds) and invalidate on Update/Create/Delete.
            var tags = await query
                .SelectMany(o => o.Tags)
                .Distinct()
                .OrderBy(t => t)
                .Take(500)
                .ToArrayAsync(cancellationToken);

            return tags;
        }
    }
}
