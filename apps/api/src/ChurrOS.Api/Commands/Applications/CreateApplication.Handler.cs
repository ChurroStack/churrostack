using ChurrOS.Api.Commands.Environment;
using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Commands.Template;
using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Applications
{
    public class CreateApplicationHandler : IRequestHandler<CreateApplication, ValueTask<ApplicationItem>>
    {
        private readonly ChurrosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly IIdGeneratorService _idGenerationService;
        private readonly IMediator _mediator;
        private readonly ICacheService _cacheService;

        public CreateApplicationHandler(ChurrosDbContext context, ITenantResolver tenantResolver, IIdGeneratorService idGenerationService, IMediator mediator, ICacheService cacheService)
        {
            _context = context;
            _tenantResolver = tenantResolver;
            _idGenerationService = idGenerationService;
            _mediator = mediator;
            _cacheService = cacheService;
        }

        public async ValueTask<ApplicationItem> Handle(CreateApplication request, CancellationToken cancellationToken)
        {
            // Validate input
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Body.Name, nameof(request.Body.Name));
            if (!NamingUtils.IsValidName(request.Body.Name))
            {
                throw new ArgumentException("The provided name is not valid. Only lowercase alphanumeric and _ are allowed.", nameof(request.Body.Name));
            }

            var environmentRef = await _mediator.Send(new GetEnvironmentIdByName(request.Body.Environment), cancellationToken);

            if (!await _mediator.Send(new IsAdminOrHasAcl(environmentRef.AclId, Permission.Execute), cancellationToken))
            {
                throw new UnauthorizedAccessException("You do not have permission to create new applications in this environment.");
            }

            var applicationQuota = _context.Quota.Applications;
            var currentApplicationCount = await _context.Set<Domain.Application>()
                .Where(a => a.AccountId == _tenantResolver.AccountId)
                .CountAsync(cancellationToken);
            if (currentApplicationCount >= applicationQuota)
            {
                throw new InvalidOperationException("Application quota exceeded for this environment.");
            }

            var templateId = await _mediator.Send(new GetTemplateIdByName(request.Body.Template, environmentRef.Type), cancellationToken);

            var aclId = _idGenerationService.CreateLongId();

            // Create ACL for the application
            _context.Set<Acl>().Add(new Acl(_tenantResolver.AccountId, aclId));
            var aclMembersRepo = _context.Set<Domain.AclMember>();
            var sysId = await _mediator.Send(new GetIdentityId("system"), cancellationToken);
            aclMembersRepo.Add(new AclMember(_tenantResolver.AccountId, aclId, sysId, Permission.Read | Permission.Write | Permission.Execute));
            aclMembersRepo.Add(new AclMember(_tenantResolver.AccountId, aclId, _context.IdentityId, Permission.Read | Permission.Write | Permission.Execute | Permission.Manage));
            await _cacheService.InvalidatePrefixAsync($"tenant:{_tenantResolver.AccountId}:identity:{_context.IdentityId}");

            // Save application to database
            var now = DateTimeOffset.UtcNow;
            var id = _idGenerationService.CreateLongId();
            var repo = _context.Set<Domain.Application>();
            repo.Add(new Domain.Application(
                accountId: _tenantResolver.AccountId,
                environmentId: environmentRef.Id,
                id: id,
                aclId: aclId,
                name: request.Body.Name,
                templateId: templateId,
                mode: request.Body.Mode,
                size: new SizeRequestItem(null, "500m", "1Gi", null, null),
                replicas: 1,
                variables: request.Body.Variables ?? [],
                parameters: new Dictionary<string, string[]>(),
                ports: [],
                deploymentHash: Array.Empty<byte>(),
                tags: [],
                metadata: request.Body.Metadata,
                createdAt: now,
                createdById: _context.IdentityId,
                modifiedAt: now,
                modifiedById: _context.IdentityId
            ));

            var appTemplate = await _mediator.Send(new GetTemplateByName(request.Body.Template, environmentRef.Type), cancellationToken);
            if (appTemplate?.Definition?.Extensions is not null)
            {
                foreach (var templateExtension in appTemplate.Definition.Extensions)
                {
                    var tempateName = templateExtension.Template;
                    // TODO: Fix this patch
                    var extensionTemplate = await _mediator.Send(new GetTemplateByName(tempateName, environmentRef.Type), cancellationToken);
                    var extensionTemplateId = await _mediator.Send(new GetTemplateIdByName(tempateName, environmentRef.Type), cancellationToken);

                    var extensionsFromRequest = request.Body.Extensions?.Where(e => e.Name == templateExtension.Name).FirstOrDefault();
                    _context.Set<Domain.ApplicationExtension>().Add(new ApplicationExtension(
                        accountId: _tenantResolver.AccountId,
                        environmentId: environmentRef.Id,
                        applicationId: id,
                        templateId: extensionTemplateId,
                        name: templateExtension.Name,
                        enabled: extensionsFromRequest?.Enabled ?? false,
                        parameters: DeployApplicationHandler.ParseParameters(extensionsFromRequest?.Enabled ?? false, templateExtension.Name, extensionTemplate.Definition.Parameters, extensionsFromRequest?.Parameters, templateExtension.Parameters?.ToDictionary(o => o.Key, o => o.Value is Array ? (string[])o.Value : [o.Value?.ToString()!])),
                        createdAt: now,
                        createdById: _context.IdentityId,
                        modifiedAt: now,
                        modifiedById: _context.IdentityId
                        )
                    );
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            await _cacheService.InvalidatePrefixAsync($"app:{request.Body.Name}");

            return await _mediator.Send(new GetApplicationByName(request.Body.Name), cancellationToken);
        }
    }
}
