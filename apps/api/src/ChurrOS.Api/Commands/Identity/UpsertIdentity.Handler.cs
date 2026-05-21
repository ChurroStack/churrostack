using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;
namespace ChurrOS.Api.Commands.Identity
{
    public class UpsertIdentityHandler : IRequestHandler<UpsertIdentity, ValueTask<IdentityWithAssignedItem>>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly ITenantResolver _tenantResolver;
        private readonly IIdGeneratorService _idGeneratorService;
        private readonly IMediator _mediator;
        private readonly ICacheService _cacheService;

        public UpsertIdentityHandler(ChurrosDbContext dbContext, ITenantResolver tenantResolver, IIdGeneratorService idGeneratorService, IMediator mediator, ICacheService cacheService)
        {
            _dbContext = dbContext;
            _tenantResolver = tenantResolver;
            _idGeneratorService = idGeneratorService;
            _mediator = mediator;
            _cacheService = cacheService;
        }

        public async ValueTask<IdentityWithAssignedItem> Handle(UpsertIdentity request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request, nameof(request));
            ArgumentNullException.ThrowIfNull(request.Body.DisplayName, nameof(request.Body.DisplayName));
            if (string.IsNullOrWhiteSpace(request.Body.Name))
            {
                if (request.Body.Type == IdentityType.Application)
                {
                    request.Body.Name = Guid.NewGuid().ToString();
                }
                else
                {
                    throw new ArgumentException("Name cannot be null or empty.");
                }
            }

            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _dbContext.IdentityId), cancellationToken);

            var identityMembersOfRepo = _dbContext.Set<Domain.IdentityMemberOf>();
            var identityRepo = _dbContext.Set<Domain.Identity>();
            var identity = await identityRepo
                .Include(o => o.MemberOf)
                .Where(o => o.Name.ToLower().Equals(request.Body.Name.ToLower()))
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            var assignedNames = new List<string>();
            var assignedIds = new List<long>();
            var idsToPurge = new List<long>();

            if (request.Body.Assigned?.Length > 0)
            {
                assignedNames = request.Body.Assigned.Select(x => x.ToLower()).ToList();
                var assignedIdsQuery = identityRepo.AsNoTracking().Where(o => assignedNames.Contains(o.Name.ToLower()));

                if (request.Body.Type == IdentityType.Group)
                {
                    assignedIdsQuery = assignedIdsQuery.Where(o => o.Type != IdentityType.Group);
                }
                else
                {
                    assignedIdsQuery = assignedIdsQuery.Where(o => o.Type == IdentityType.Group);
                }

                assignedIds = await assignedIdsQuery.Select(o => o.Id).ToListAsync(cancellationToken: cancellationToken);
                idsToPurge.AddRange(assignedIds);
            }

            if (identity is null)
            {
                var now = DateTimeOffset.Now;
                identity = new Domain.Identity(_tenantResolver.AccountId, _idGeneratorService.CreateLongId(), request.Body.Name, request.Body.DisplayName, request.Body.Type, request.Body.Role, now, _dbContext.IdentityId, now, _dbContext.IdentityId);
                identityRepo.Add(identity);

                if (request.Body.Type == IdentityType.Group)
                {
                    foreach (var assignedId in assignedIds)
                    {
                        var memberOf = new Domain.IdentityMemberOf(_tenantResolver.AccountId, assignedId, identity.Id);
                        identityMembersOfRepo.Add(memberOf);
                    }
                }
                else
                {
                    foreach (var assignedId in assignedIds)
                    {
                        var memberOf = new Domain.IdentityMemberOf(_tenantResolver.AccountId, identity.Id, assignedId);
                        identityMembersOfRepo.Add(memberOf);
                    }
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(request.IfNoneMatch))
                {
                    if (request.IfNoneMatch != "*")
                        throw new ArgumentException("Invalid If-None-Match header.");

                    throw new HttpException(412, "Identity already exists", identity.Id.ToString());
                }
                identity.SetRole(request.Body.Role);
                identity.SetDisplayName(request.Body.DisplayName);
                identity.SetModified(_dbContext.IdentityId, DateTimeOffset.Now);

                if (request.Body.Type == IdentityType.Group)
                {
                    var itemsToDelete = identityMembersOfRepo.Where(o => o.GroupId == identity.Id);
                    identityMembersOfRepo.RemoveRange(itemsToDelete);

                    idsToPurge.AddRange(itemsToDelete.Select(x => x.IdentityId));
                    idsToPurge.AddRange(itemsToDelete.Select(x => x.GroupId));

                    foreach (var assignedId in assignedIds)
                    {
                        var memberOf = new Domain.IdentityMemberOf(_tenantResolver.AccountId, assignedId, identity.Id);
                        identityMembersOfRepo.Add(memberOf);
                    }
                }
                else
                {
                    var itemsToDelete = identityMembersOfRepo.Where(o => o.IdentityId == identity.Id);
                    identityMembersOfRepo.RemoveRange(itemsToDelete);

                    idsToPurge.AddRange(itemsToDelete.Select(x => x.IdentityId));
                    idsToPurge.AddRange(itemsToDelete.Select(x => x.GroupId));

                    foreach (var assignedId in assignedIds)
                    {
                        var memberOf = new Domain.IdentityMemberOf(_tenantResolver.AccountId, identity.Id, assignedId);
                        identityMembersOfRepo.Add(memberOf);
                    }
                }
            }

            idsToPurge.Add(identity.Id);
            idsToPurge.AddRange(identity.MemberOf.Select(x => x.IdentityId));

            var clientSecret = string.Empty;

            await _dbContext.SaveChangesAsync();

            var toPurge = await identityRepo.AsNoTracking().Where(o => idsToPurge.Distinct().Contains(o.Id)).ToListAsync();
            foreach (var identityToPurge in toPurge)
            {
                await _cacheService.InvalidatePrefixAsync($"identity:{identityToPurge.Name.ToLower()}:tenant:default");
                await _cacheService.InvalidatePrefixAsync($"tenant:{_tenantResolver.AccountId}:identity:{identityToPurge.Id}");
                await _cacheService.InvalidatePrefixAsync($"tenant:{_tenantResolver.AccountId}:identity:{identityToPurge.Name.ToLower()}");
            }

            var newIdentity = await _mediator.Send(new GetIdentity(identity.Name), cancellationToken);

            return newIdentity;
        }
    }
}
