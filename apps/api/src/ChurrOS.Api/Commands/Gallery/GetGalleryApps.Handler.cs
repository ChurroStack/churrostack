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
    public class GetGalleryAppsHandler : IRequestHandler<GetGalleryApps, ValueTask<QueryResult<GalleryAppSummary>>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;

        public GetGalleryAppsHandler(ChurrosDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async ValueTask<QueryResult<GalleryAppSummary>> Handle(GetGalleryApps request, CancellationToken cancellationToken)
        {
            var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Execute), cancellationToken);

            var envIds = await _context.Set<Domain.Environment>()
                .Where(o => identityAcls.Keys.Contains(o.AclId))
                .Select(o => o.Id)
                .ToListAsync();

            var query = _context.Set<Domain.Application>()
                .Where(o => identityAcls.Keys.Contains(o.AclId) || envIds.Contains(o.EnvironmentId))
                .Include(o => o.CreatedBy)
                .Include(o => o.ModifiedBy)
                .Select(o => new { o.Name, o.Template, o.Metadata, o.Ports, o.CreatedAt, o.CreatedBy, o.ModifiedAt, o.ModifiedBy })
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.Query?.Search))
            {
                query = query.Where(o => o.Name!.Contains(request.Query.Search));
            }

            var count = await query.CountAsync(cancellationToken);

            query = request.Query?.ApplyPaginationTo(query) ?? query;

            var items = await query
                .ToListAsync();

            return new QueryResult<GalleryAppSummary>(items.Select(o =>
            {
                string description = string.Empty;
                if (o.Metadata?.TryGetProperty("description", out var desc) ?? false)
                {
                    description = desc.GetString() ?? string.Empty;
                }
                var port = o.Ports?.FirstOrDefault(o => o.Protocol == Models.Dtos.Template.Definition.ProtocolType.Web && o.Sharing == Models.Dtos.Share.SharingMode.Members);
                string? path = port is null ? null : port.Uri ?? $"share/{o.Name}/{port.Name}";
                return new GalleryAppSummary(o.Template?.Icon, o.Name, o.Template?.Title, description, path);
            }), count);
        }
    }
}
