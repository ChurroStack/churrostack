using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Identity
{
    public class IsAdminOrHasAclHandler : IRequestHandler<IsAdminOrHasAcl, ValueTask<bool>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;

        public IsAdminOrHasAclHandler(ChurrosDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async ValueTask<bool> Handle(IsAdminOrHasAcl request, CancellationToken cancellationToken)
        {
            if (await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken))
                return true;

            var acls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, request.Permission), cancellationToken);
            return acls.ContainsKey(request.AclId);
        }
    }
}
