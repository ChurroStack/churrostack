using ChurrunKubernetes.Data;
using ChurrunKubernetes.Models.Dtos;
using ChurrunKubernetes.Models.Dtos.Template;
using DispatchR.Abstractions.Send;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrunKubernetes.Commands.Template
{
    public class GetTemplatesHandler : IRequestHandler<GetTemplates, ValueTask<QueryResult<TemplateSummary>>>
    {
        private readonly ChurrunDbContext _context;
        private readonly IMapper _mapper;

        public GetTemplatesHandler(ChurrunDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async ValueTask<QueryResult<TemplateSummary>> Handle(GetTemplates request, CancellationToken cancellationToken)
        {
            var query = _context.Set<Domain.Template>()
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.Query?.Search))
            {
                query = query.Where(o => o.Name!.Contains(request.Query.Search));
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