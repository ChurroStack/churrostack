using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Environment;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Environment
{
    public class UpdateEnvironmentHandler : IRequestHandler<UpdateEnvironment, ValueTask<EnvironmentItem>>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly IMediator _mediator;
        private readonly ClientNotificationService _clientNotificationService;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICacheService _cacheService;
        private readonly ILogger<UpdateEnvironmentHandler> _logger;

        public UpdateEnvironmentHandler(
            ChurrosDbContext dbContext,
            IMediator mediator,
            ClientNotificationService clientNotificationService,
            ITenantResolver tenantResolver,
            ICacheService cacheService,
            ILogger<UpdateEnvironmentHandler> logger)
        {
            _dbContext = dbContext;
            _mediator = mediator;
            _clientNotificationService = clientNotificationService;
            _tenantResolver = tenantResolver;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async ValueTask<EnvironmentItem> Handle(UpdateEnvironment request, CancellationToken cancellationToken)
        {
            var environment = await _dbContext.Set<Domain.Environment>().FirstOrDefaultAsync(o => o.Name == request.Name);

            if (environment is null)
                throw new NotFoundException();

            if (!await _mediator.Send(new IsAdminOrHasAcl(environment.AclId, Permission.Manage), cancellationToken))
            {
                _logger.LogInformation("[Authorization] accountId={AccountId} identityId={IdentityId} action=UpdateEnvironment environmentAclId={AclId} decision=denied",
                    _tenantResolver.AccountId, _dbContext.IdentityId, environment.AclId);
                throw new UnauthorizedAccessException("You do not have permission to manage this environment's access.");
            }

            _logger.LogInformation("[Authorization] accountId={AccountId} identityId={IdentityId} action=UpdateEnvironment environmentAclId={AclId} decision=allowed",
                _tenantResolver.AccountId, _dbContext.IdentityId, environment.AclId);

            environment.ModifiedAt = DateTimeOffset.Now;
            environment.ModifiedById = _dbContext.IdentityId;

            if (request.Body.Tags is not null)
                environment.Tags = TagsHelper.Normalize(request.Body.Tags);

            long[]? updatedMembers = null;
            List<long>? membersToPurge = null;
            if (request.Body.Members is not null)
            {
                membersToPurge = await _dbContext.Set<Domain.AclMember>()
                    .Include(o => o.Identity)
                    .Where(o => o.AclId == environment.AclId)
                    .Select(o => o.Identity!.Id)
                    .ToListAsync(cancellationToken);

                updatedMembers = await _mediator.UpdateAclAsync(membersToPurge, _dbContext, _tenantResolver.AccountId, environment.AclId, request.Body.Members, _logger, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _clientNotificationService.NotifyChangeAsync(environment.AccountId, environment.Name, ClientNotificationService.NotificationTargetType.Environment, cancellationToken);

            if (updatedMembers is not null)
            {
                await _cacheService.InvalidateIdentityAuthorizationCachesAsync(
                    _dbContext,
                    _tenantResolver.AccountId,
                    (membersToPurge ?? []).Union(updatedMembers),
                    _logger,
                    cancellationToken);
            }

            return await _mediator.Send(new GetEnvironmentByName(request.Name), cancellationToken);
        }
    }
}
