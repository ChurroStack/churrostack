using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR;
using DispatchR.Abstractions.Send;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetLatestApplicationEventsHandler : IRequestHandler<GetLatestApplicationEvents, ValueTask<QueryResult<ApplicationEventItem>>>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly IMediator _mediator;
        private readonly IMapper _mapper;

        public GetLatestApplicationEventsHandler(
            ChurrosDbContext dbContext,
            IMediator mediator,
            IMapper mapper)
        {
            _dbContext = dbContext;
            _mediator = mediator;
            _mapper = mapper;
        }

        public async ValueTask<QueryResult<ApplicationEventItem>> Handle(GetLatestApplicationEvents request, CancellationToken cancellationToken)
        {
            var app = await _mediator.Send(new GetApplicationIdByName(request.Name), cancellationToken);

            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _dbContext.IdentityId), cancellationToken);
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_dbContext.IdentityId, Permission.Read), cancellationToken);
                if (!identityAcls.ContainsKey(app.AclId) && !identityAcls.ContainsKey(app.EnvironmentAclId))
                    throw new UnauthorizedAccessException("You do not have permission to read this application.");
            }

            var query = _dbContext.Set<ApplicationEvent>()
                .AsNoTracking()
                .Where(e => e.ApplicationId == app.Id);

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                query = query.Where(e => e.Type.StartsWith(request.Search) || e.Target.StartsWith(request.Search) || e.Reason.StartsWith(request.Search) || e.Message.StartsWith(request.Search));
            }

            var result = query.OrderByDescending(e => e.Timestamp)
                .Take(request.Take ?? 50);

            var items = await _mapper
                .From(query)
                .ProjectToType<ApplicationEventItem>()
                .ToListAsync();

            return new QueryResult<ApplicationEventItem>(items);
        }
    }
}
