using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Llm;
using ChurrOS.Api.Services;
using DispatchR;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Llm
{
    public class CreateLlmHandler : IRequestHandler<CreateLlm, ValueTask<LlmItem>>
    {
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _dbContext;
        private readonly IIdGeneratorService _idGenerationService;
        private readonly IMapper _mapper;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICacheService _cacheService;

        public CreateLlmHandler(IMediator mediator, ChurrosDbContext dbContext, IIdGeneratorService idGenerationService, IMapper mapper, ITenantResolver tenantResolver, ICacheService cacheService)
        {
            _mediator = mediator;
            _dbContext = dbContext;
            _idGenerationService = idGenerationService;
            _mapper = mapper;
            _tenantResolver = tenantResolver;
            _cacheService = cacheService;
        }

        public async ValueTask<LlmItem> Handle(CreateLlm request, CancellationToken cancellationToken)
        {
            // Validate input
            if (request.Body.Names is null || request.Body.Names.Length == 0 || string.IsNullOrWhiteSpace(request.Body.Names[0]))
            {
                throw new ArgumentException("The LLM must have at least one name.", nameof(request.Body.Names));
            }

            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _dbContext.IdentityId), cancellationToken);

            var repo = _dbContext.Set<Domain.Llm>();
            var id = _idGenerationService.CreateLongId();
            var now = DateTimeOffset.UtcNow;
            var aclId = _idGenerationService.CreateLongId();
            _dbContext.Set<Acl>().Add(new Acl(_tenantResolver.AccountId, aclId));
            var aclMembersRepo = _dbContext.Set<Domain.AclMember>();
            var sysId = await _mediator.Send(new GetIdentityId("system"), cancellationToken);
            aclMembersRepo.Add(new AclMember(_tenantResolver.AccountId, aclId, sysId, Permission.Read | Permission.Write | Permission.Execute));
            aclMembersRepo.Add(new AclMember(_tenantResolver.AccountId, aclId, _dbContext.IdentityId, Permission.Read | Permission.Write | Permission.Execute | Permission.Manage));

            repo.Add(new Domain.Llm(_tenantResolver.AccountId, id, request.Body.Names, aclId, LLmRoutingType.RoundRobin, [], null, now, _dbContext.IdentityId, now, _dbContext.IdentityId, new Dictionary<string, bool>()));

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _cacheService.InvalidatePrefixAsync($"tenant:{_tenantResolver.AccountId}:identity:{_dbContext.IdentityId}");

            // Retrieve and return created environment
            var llm = await repo
                .Include(o => o.CreatedBy)
                .Include(o => o.ModifiedBy)
                .FirstOrDefaultAsync(o => o.Id == id);

            return _mapper.Map<Domain.Llm, LlmItem>(llm!);
        }
    }
}
