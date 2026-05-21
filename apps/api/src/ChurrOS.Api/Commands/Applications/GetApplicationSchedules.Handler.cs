using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetApplicationSchedulesHandler : IRequestHandler<GetApplicationSchedules, ValueTask<QueryResult<ApplicationScheduleItem>>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;

        public GetApplicationSchedulesHandler(ChurrosDbContext context, IMapper mapper, IMediator mediator)
        {
            _context = context;
            _mapper = mapper;
            _mediator = mediator;
        }

        public async ValueTask<QueryResult<ApplicationScheduleItem>> Handle(GetApplicationSchedules request, CancellationToken cancellationToken)
        {
            var appRepo = _context.Set<Domain.Application>();
            var schedulerRepo = _context.Set<Domain.ApplicationSchedule>();

            var app = await appRepo
                .AsNoTracking()
                .Include(o => o.Environment)
                .Where(o => o.Name == request.Name)
                .Select(o => new { o.Id, o.AclId, EnvironmentAclId = o.Environment.AclId })
                .FirstOrDefaultAsync(cancellationToken);

            if (app == null)
            {
                throw new NotFoundException($"Application with name '{request.Name}' was not found.");
            }

            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Read), cancellationToken);
                if (!identityAcls.ContainsKey(app.AclId) && !identityAcls.ContainsKey(app.EnvironmentAclId))
                    throw new UnauthorizedAccessException("You do not have permission to read this application.");
            }

            var query = schedulerRepo
                .Where(o => o.ApplicationId == app.Id);

            var items = await _mapper
                .From(query)
                .ProjectToType<ApplicationScheduleItem>()
                .ToListAsync();

            return new QueryResult<ApplicationScheduleItem>(items, items.Count);
        }
    }
}
