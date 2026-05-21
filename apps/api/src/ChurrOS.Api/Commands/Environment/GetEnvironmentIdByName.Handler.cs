using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Environment;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Environment
{
    public class GetEnvironmentIdByNameHandler : IRequestHandler<GetEnvironmentIdByName, ValueTask<EnvironmentIdentifier>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;

        public GetEnvironmentIdByNameHandler(ChurrosDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async ValueTask<EnvironmentIdentifier> Handle(GetEnvironmentIdByName request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Environment>();
            var item = await repo
                .AsNoTracking()
                .Include(o => o.Account)
                .Include(o => o.CreatedBy)
                .Include(o => o.ModifiedBy)
                .Where(o => o.Name == request.Name)
                .Select(o => new { o.Id, o.AclId, o.Type })
                .FirstOrDefaultAsync();

            if (item == null)
            {
                throw new NotFoundException($"Environment with name '{request.Name}' was not found.");
            }

            // Get current user's ACLs
            var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Read), cancellationToken);

            // Ensure user has Read (+r) in the ACL
            if (!identityAcls.Keys.Contains(item.AclId))
                throw new UnauthorizedAccessException();

            return new EnvironmentIdentifier(item.Id, item.AclId, item.Type);
        }
    }
}
