using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.ApiKey;
using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR;
using DispatchR.Abstractions.Send;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.ApiKeys
{
    public class GetApiKeysHandler : IRequestHandler<GetApiKeys, ValueTask<QueryResult<ApiKeyItem>>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;

        public GetApiKeysHandler(
            ChurrosDbContext context,
            IMapper mapper,
            IMediator mediator)
        {
            _context = context;
            _mapper = mapper;
            _mediator = mediator;
        }

        public async ValueTask<QueryResult<ApiKeyItem>> Handle(GetApiKeys request, CancellationToken cancellationToken)
        {
            var isAdministrator = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            var query = _context.Set<Domain.ApiKey>()
                .Include(o => o.Identity)
                .AsQueryable();

            if (!isAdministrator)
            {
                var identityAcls = (await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Manage), cancellationToken)).Select(o => o.Key).ToArray();
                var additionalIdentities = await _context
                    .Set<Domain.Identity>()
                    .Where(o => identityAcls.Contains(o.AclId!.Value) || o.CreatedById == _context.IdentityId)
                    .Select(o => o.Id)
                    .ToListAsync(cancellationToken);
                additionalIdentities = additionalIdentities.Union([_context.IdentityId]).ToList();
                query = query.Where(x => additionalIdentities.Contains(x.IdentityId));
            }

            if (!string.IsNullOrWhiteSpace(request.Query?.Search))
            {
                query = query.Where(o => o.Description!.Contains(request.Query.Search) || o.Identity!.Name.Contains(request.Query.Search));
            }

            var count = await query.CountAsync(cancellationToken);

            query = request.Query?.ApplyPaginationTo(query) ?? query;

            var items = await _mapper
                .From(query)
                .ProjectToType<ApiKeyItem>()
                .ToListAsync();

            return new QueryResult<ApiKeyItem>(items, count);
        }
    }
}
