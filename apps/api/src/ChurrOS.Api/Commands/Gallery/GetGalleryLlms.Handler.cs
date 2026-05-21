using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Gallery;
using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Gallery
{
    public class GetGalleryLlmsHandler : IRequestHandler<GetGalleryLlms, ValueTask<QueryResult<GalleryLlmSummary>>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;

        public GetGalleryLlmsHandler(ChurrosDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async ValueTask<QueryResult<GalleryLlmSummary>> Handle(GetGalleryLlms request, CancellationToken cancellationToken)
        {
            var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Execute), cancellationToken);

            var query = _context.Set<Domain.Llm>()
                .AsNoTracking()
                .Where(o => identityAcls.Keys.Contains(o.AclId))
                .Include(o => o.CreatedBy)
                .Include(o => o.ModifiedBy)
                .Select(o => new { o.Names, o.CreatedAt, o.CreatedBy, o.ModifiedAt, o.ModifiedBy });

            var count = await query.CountAsync(cancellationToken);

            query = request.Query?.ApplyPaginationTo(query) ?? query;

            var items = await query
                .ToListAsync();

            return new QueryResult<GalleryLlmSummary>(items.Select(o =>
            {
                return new GalleryLlmSummary(null, o.Names);
            }), count);
        }
    }
}
