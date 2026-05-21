using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetApplicationByNameHandler : IRequestHandler<GetApplicationByName, ValueTask<ApplicationItem>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;

        public GetApplicationByNameHandler(ChurrosDbContext context, IMapper mapper, IMediator mediator)
        {
            _context = context;
            _mapper = mapper;
            _mediator = mediator;
        }

        public async ValueTask<ApplicationItem> Handle(GetApplicationByName request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Application>();

            var item = await repo
                .AsNoTracking()
                .Include(o => o.Environment)
                .Include(o => o.Extensions)
                .Include(o => o.Deployments)
                .Include(o => o.Template)
                .Include(o => o.CreatedBy)
                .Include(o => o.ModifiedBy)
                .Include("Acl.Members.Identity")
                .FirstOrDefaultAsync(o => o.Name == request.Name);

            if (item == null)
            {
                throw new NotFoundException($"Application with name '{request.Name}' was not found.");
            }

            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Read), cancellationToken);
                if (!identityAcls.ContainsKey(item.AclId) && !identityAcls.ContainsKey(item.Environment!.AclId))
                    throw new UnauthorizedAccessException("You do not have permission to read this application.");
            }

            return _mapper.Map<Domain.Application, ApplicationItem>(item!);
        }
    }
}
