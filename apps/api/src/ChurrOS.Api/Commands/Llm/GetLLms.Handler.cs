using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Llm;
using DispatchR;
using DispatchR.Abstractions.Send;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetLLmsHandler : IRequestHandler<GetLlms, ValueTask<QueryResult<LlmSummary>>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;

        public GetLLmsHandler(ChurrosDbContext context, IMapper mapper, IMediator mediator)
        {
            _context = context;
            _mapper = mapper;
            _mediator = mediator;
        }

        public async ValueTask<QueryResult<LlmSummary>> Handle(GetLlms request, CancellationToken cancellationToken)
        {
            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            long[]? aclIds = null;
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Read), cancellationToken);
                aclIds = identityAcls.Keys.ToArray();
            }

            IQueryable<Domain.Llm> query = _context.Set<Domain.Llm>()
                .Include(o => o.CreatedBy)
                .Include(o => o.ModifiedBy)
                .AsNoTracking();

            if (!isAdmin)
                query = query.Where(o => aclIds!.Contains(o.AclId));

            if (!string.IsNullOrWhiteSpace(request.Query?.Search))
            {
                query = query.Where(o => o.Names.Any(x => x.Contains(request.Query.Search)));
            }

            var count = await query.CountAsync(cancellationToken);

            query = request.Query?.ApplyPaginationTo(query) ?? query;

            var items = await _mapper
                .From(query)
                .ProjectToType<LlmSummary>()
                .ToListAsync();

            return new QueryResult<LlmSummary>(items, count);
        }
    }
}
