using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Template;
using DispatchR;
using DispatchR.Abstractions.Send;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Template
{
    public class GetTemplatesHandler : IRequestHandler<GetTemplates, ValueTask<QueryResult<TemplateSummary>>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;

        public GetTemplatesHandler(ChurrosDbContext context, IMapper mapper, IMediator mediator)
        {
            _context = context;
            _mapper = mapper;
            _mediator = mediator;
        }

        public async ValueTask<QueryResult<TemplateSummary>> Handle(GetTemplates request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new EnsureHasRole(IdentityRole.User, _context.IdentityId), cancellationToken);

            var query = _context.Set<Domain.Template>()
                .Include(o => o.CreatedBy)
                .Include(o => o.ModifiedBy)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.Query?.Search))
            {
                query = query.Where(o => o.Name!.Contains(request.Query.Search));
            }

            if (request.Query?.Type.HasValue ?? false)
            {
                var type = request.Query.Type.Value.ToString().ToLowerInvariant();
                query = query.Where(o => o.Type == type);
            }

            var count = await query.CountAsync(cancellationToken);

            query = request.Query?.ApplyPaginationTo(query) ?? query;

            var items = await _mapper
                .From(query)
                .ProjectToType<TemplateSummary>()
                .ToListAsync();

            return new QueryResult<TemplateSummary>(items, count);
        }
    }
}
