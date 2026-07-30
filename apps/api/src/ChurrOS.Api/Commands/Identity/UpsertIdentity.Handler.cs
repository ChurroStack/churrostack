using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
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
        private readonly ILogger<UpsertIdentityHandler> _logger;

        public UpsertIdentityHandler(ChurrosDbContext dbContext, ITenantResolver tenantResolver, IIdGeneratorService idGeneratorService, IMediator mediator, ICacheService cacheService, ILogger<UpsertIdentityHandler> logger)
        {
            _dbContext = dbContext;
            _tenantResolver = tenantResolver;
            _idGeneratorService = idGeneratorService;
            _mediator = mediator;
            _cacheService = cacheService;
            _logger = logger;
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

                var (removedIds, addedIds) = await UpdateMembershipAsync(identity.Id, assignedIds, request.Body.Type == IdentityType.Group, cancellationToken);
                idsToPurge.AddRange(removedIds);
                idsToPurge.AddRange(addedIds);
            }

            idsToPurge.Add(identity.Id);

            await _dbContext.SaveChangesAsync(cancellationToken);

            var idsToPurgeDistinct = idsToPurge.Distinct().ToArray();
            var toPurge = await identityRepo.AsNoTracking().Where(o => idsToPurgeDistinct.Contains(o.Id)).ToListAsync(cancellationToken);
            foreach (var identityToPurge in toPurge)
            {
                await _cacheService.InvalidatePrefixAsync($"identity:{identityToPurge.Name.ToLower()}:tenant:default");
                await _cacheService.InvalidatePrefixAsync($"tenant:{_tenantResolver.AccountId}:identity:{identityToPurge.Name.ToLower()}");
            }
            await _cacheService.InvalidateIdentityAuthorizationCachesAsync(_dbContext, _tenantResolver.AccountId, idsToPurgeDistinct, _logger, cancellationToken);

            var newIdentity = await _mediator.Send(new GetIdentity(identity.Name), cancellationToken);

            return newIdentity;
        }

        internal async Task<(long[] RemovedIds, long[] AddedIds)> UpdateMembershipAsync(long identityId, IReadOnlyCollection<long> desiredIds, bool identityIsGroup, CancellationToken cancellationToken)
        {
            var currentMemberships = identityIsGroup
                ? await _dbContext.Set<Domain.IdentityMemberOf>()
                    .Where(o => o.GroupId == identityId)
                    .ToListAsync(cancellationToken)
                : await _dbContext.Set<Domain.IdentityMemberOf>()
                    .Where(o => o.IdentityId == identityId)
                    .ToListAsync(cancellationToken);

            var currentIds = currentMemberships
                .Select(o => identityIsGroup ? o.IdentityId : o.GroupId)
                .ToArray();
            var currentIdsSet = currentIds.ToHashSet();
            var desiredIdsSet = desiredIds.ToHashSet();
            var removedIds = currentIdsSet.Except(desiredIdsSet).ToArray();
            var addedIds = desiredIdsSet.Except(currentIdsSet).ToArray();

            if (removedIds.Length > 0)
            {
                _dbContext.Set<Domain.IdentityMemberOf>().RemoveRange(
                    currentMemberships.Where(o => removedIds.Contains(identityIsGroup ? o.IdentityId : o.GroupId)));
            }

            foreach (var addedId in addedIds)
            {
                _dbContext.Set<Domain.IdentityMemberOf>().Add(identityIsGroup
                    ? new Domain.IdentityMemberOf(_tenantResolver.AccountId, addedId, identityId)
                    : new Domain.IdentityMemberOf(_tenantResolver.AccountId, identityId, addedId));
            }

            _logger.LogInformation("[IdentityMembership] accountId={AccountId} identityId={IdentityId} type={IdentityType} added={AddedCount} removed={RemovedCount}",
                _tenantResolver.AccountId, identityId, identityIsGroup ? "group" : "identity", addedIds.Length, removedIds.Length);

            return (removedIds, addedIds);
        }
    }
}
