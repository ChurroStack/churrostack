using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Environment;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using DispatchR;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ChurrOS.Api.Commands.Environment
{
    public class CreateEnvironmentHandler : IRequestHandler<CreateEnvironment, ValueTask<EnvironmentItem>>
    {
        private readonly ChurrosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly IIdGeneratorService _idGenerationService;
        private readonly ICacheService _cacheService;
        private readonly IMapper _mapper;
        readonly IMediator _mediator;
        private readonly IConfiguration _configuration;

        public CreateEnvironmentHandler(ChurrosDbContext context, ITenantResolver tenantResolver, IIdGeneratorService idGenerationService, IMapper mapper, IMediator mediator, ICacheService cacheService, IConfiguration configuration)
        {
            _context = context;
            _tenantResolver = tenantResolver;
            _idGenerationService = idGenerationService;
            _mapper = mapper;
            _mediator = mediator;
            _cacheService = cacheService;
            _configuration = configuration;
        }

        public async ValueTask<EnvironmentItem> Handle(CreateEnvironment request, CancellationToken cancellationToken)
        {
            // Validate input
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Body.Name, nameof(request.Body.Name));
            if (!NamingUtils.IsValidName(request.Body.Name))
            {
                throw new ArgumentException("The provided name is not valid. Only lowercase alphanumeric and _ are allowed.", nameof(request.Body.Name));
            }

            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);

            var environmentQuota = _context.Quota.Environments;
            var currentEnvironmentCount = await _context.Set<Domain.Environment>()
                .Where(a => a.AccountId == _tenantResolver.AccountId)
                .CountAsync(cancellationToken);
            if (currentEnvironmentCount >= environmentQuota)
            {
                throw new InvalidOperationException("Environment quota exceeded for this environment.");
            }

            // Create ACL for the environment
            var aclId = _idGenerationService.CreateLongId();
            _context.Set<Acl>().Add(new Acl(_tenantResolver.AccountId, aclId));
            var aclMembersRepo = _context.Set<Domain.AclMember>();
            var sysId = await _mediator.Send(new GetIdentityId("system"), cancellationToken);
            aclMembersRepo.Add(new AclMember(_tenantResolver.AccountId, aclId, sysId, Permission.Read | Permission.Write | Permission.Execute));
            aclMembersRepo.Add(new AclMember(_tenantResolver.AccountId, aclId, _context.IdentityId, Permission.Read | Permission.Write | Permission.Execute | Permission.Manage));
            await _cacheService.InvalidatePrefixAsync($"tenant:{_tenantResolver.AccountId}:identity:{_context.IdentityId}");

            // Save environment to database
            var now = DateTimeOffset.UtcNow;
            var id = _idGenerationService.CreateLongId();
            var environmentRepo = _context.Set<Domain.Environment>();
            string[] host = [_configuration["Tunnel:Host:Public"]!, _configuration["Tunnel:Host:Internal"]!];
            environmentRepo.Add(new Domain.Environment(
                accountId: _tenantResolver.AccountId,
                id: id,
                name: request.Body.Name,
                type: "com.churrostack.environment.kubernetes",
                host: host,
                port: GetAvaiablePort(_context, host[0]),
                aclId: aclId,
                provisionStatus: EnvironmentProvisionStatus.Pending,
                sshPublicKey: [],
                encryptionKey: "",
                definition: null,
                tags: [],
                metadata: JsonDocument.Parse("{}").RootElement,
                createdAt: now,
                createdById: _context.IdentityId,
                modifiedAt: now,
                modifiedById: _context.IdentityId
            ));

            await _context.SaveChangesAsync(cancellationToken);

            // Retrieve and return created environment
            var environment = await environmentRepo
                .Include(o => o.CreatedBy)
                .Include(o => o.ModifiedBy)
                .FirstOrDefaultAsync(o => o.Id == id);
            return _mapper.Map<Domain.Environment, EnvironmentItem>(environment!);
        }

        private int GetAvaiablePort(ChurrosDbContext context, string host)
        {
            var rnd = new Random();
            int port = 0;
            while (true)
            {
                port = rnd.Next(10000, 50000);
                var exists = context.Set<Domain.Environment>().Any(e => e.Host.Contains(host) && e.Port == port);
                if (!exists)
                {
                    break;
                }
            }
            return port;
        }
    }
}
