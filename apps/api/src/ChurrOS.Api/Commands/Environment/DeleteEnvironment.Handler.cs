using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Environment
{
    public class DeleteEnvironmentHandler : IRequestHandler<DeleteEnvironment, Task>
    {
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;
        private readonly ICacheService _cacheService;

        public DeleteEnvironmentHandler(IMediator mediator, ChurrosDbContext context, ICacheService cacheService)
        {
            _mediator = mediator;
            _context = context;
            _cacheService = cacheService;
        }

        public async Task Handle(DeleteEnvironment request, CancellationToken cancellationToken)
        {
            var env = await _context.Set<Domain.Environment>()
                .AsNoTracking()
                .Where(o => o.Name == request.Name)
                .Select(o => new { o.Id, o.AclId, o.Name })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (env is null)
                throw new NotFoundException();

            if (!await _mediator.Send(new IsAdminOrHasAcl(env.AclId, Permission.Manage), cancellationToken))
                throw new UnauthorizedAccessException();

            // TODO: Deallocate everything related to this environment

            await _context.Set<Acl>().Where(o => o.Id == env.AclId).ExecuteDeleteAsync();

            var aclsFromApps = await _context.Set<Domain.Application>()
                .AsNoTracking()
                .Where(o => o.EnvironmentId == env.Id)
                .Select(o => new { o.AclId, o.Ports })
                .ToListAsync();

            long[] aclsToDelete = [.. aclsFromApps.Select(o => o.AclId).Append(env.AclId).Distinct()];

            await _context.Set<Domain.Environment>().Where(o => o.Name == request.Name).ExecuteDeleteAsync();
            await _context.Set<Domain.Acl>().Where(o => aclsToDelete.Contains(o.Id)).ExecuteDeleteAsync();

            await _cacheService.InvalidatePrefixAsync($"env:{env.Name}");
        }
    }
}
