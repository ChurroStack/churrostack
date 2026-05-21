using ChurrOS.Api.Data;
using DispatchR;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Identity
{
    public class EnsureHasRoleHandler : IRequestHandler<EnsureHasRole, Task>
    {
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _dbContext;

        public EnsureHasRoleHandler(IMediator mediator, ChurrosDbContext dbContext)
        {
            _mediator = mediator;
            _dbContext = dbContext;
        }

        public async Task Handle(EnsureHasRole request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!await _mediator.Send(new HasRole(request.Role, request.IdentityId ?? _dbContext.IdentityId), cancellationToken))
            {
                throw new UnauthorizedAccessException("Sorry but you do not have sufficient permission to run this action." /*LocalizationService.GetString("UserDoesNotHavePrivileges", request.IdentityId ?? _dbContext.IdentityId, request.Role)*/);
            }
        }
    }
}
