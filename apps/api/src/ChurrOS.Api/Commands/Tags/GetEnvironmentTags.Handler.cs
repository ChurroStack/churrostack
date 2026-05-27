using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Tags
{
    public class GetEnvironmentTagsHandler : IRequestHandler<GetEnvironmentTags, ValueTask<string[]>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;

        public GetEnvironmentTagsHandler(ChurrosDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async ValueTask<string[]> Handle(GetEnvironmentTags request, CancellationToken cancellationToken)
        {
            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);

            IQueryable<Domain.Environment> query = _context.Set<Domain.Environment>().AsNoTracking();

            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, request.Permission), cancellationToken);
                var aclIds = identityAcls.Keys.ToArray();
                query = query.Where(o => aclIds.Contains(o.AclId));
            }

            // Cap at 500 distinct tags — see GetApplicationTagsHandler for rationale.
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
