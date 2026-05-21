using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Identity
{
    public class DeleteIdentityHandler : IRequestHandler<DeleteIdentity, Task>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICacheService _cacheService;
        private readonly IMediator _mediator;

        public DeleteIdentityHandler(ChurrosDbContext dbContext, ICacheService cacheService, ITenantResolver tenantResolver, IMediator mediator)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _tenantResolver = tenantResolver;
            _mediator = mediator;
        }

        public async Task Handle(DeleteIdentity request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request, nameof(request));
            ArgumentNullException.ThrowIfNull(request.IdentityName, nameof(request.IdentityName));

            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _dbContext.IdentityId), cancellationToken);

            var identityMembersOfRepo = _dbContext.Set<Domain.IdentityMemberOf>();
            var identityRepo = _dbContext.Set<Domain.Identity>();
            var identity = await identityRepo
                .Where(o => o.Name.ToLower().Equals(request.IdentityName.ToLower()))
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (identity is null)
            {
                throw new NotFoundException($"Identity '{request.IdentityName}' was not found.");
            }

            var identityId = identity.Id;
            if (identity.Type == IdentityType.Group)
            {
                await identityMembersOfRepo.Where(o => o.GroupId == identityId).ExecuteDeleteAsync(cancellationToken: cancellationToken);
            }
            else
            {
                await identityMembersOfRepo.Where(o => o.IdentityId == identityId).ExecuteDeleteAsync(cancellationToken: cancellationToken);
            }

            //if (identity.Type == IdentityType.Application)
            //{
            //    await _identityManagerClient.DeleteClient(identity.Name, cancellationToken);
            //}

            identityRepo.Remove(identity);
            await _dbContext.SaveChangesAsync();

            //TODO: invalidar caches??
            //await _cacheService.InvalidatePrefixAsync($"tenant:{_tenantResolver.AccountId}:identity:{identityId}");
        }
    }
}
