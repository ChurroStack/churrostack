using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Identity
{
    public class GetIdentityHandler : IRequestHandler<GetIdentity, ValueTask<IdentityWithAssignedItem>>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICacheService _cacheService;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;

        public GetIdentityHandler(ChurrosDbContext dbContext, ITenantResolver tenantResolver, ICacheService cacheService, IMapper mapper, IMediator mediator)
        {
            _dbContext = dbContext;
            _tenantResolver = tenantResolver;
            _cacheService = cacheService;
            _mapper = mapper;
            _mediator = mediator;
        }

        public async ValueTask<IdentityWithAssignedItem> Handle(GetIdentity request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request, nameof(request));
            ArgumentNullException.ThrowIfNull(request.IdentityName, nameof(request.IdentityName));

            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _dbContext.IdentityId), cancellationToken);

            return await _cacheService.GetOrAddAsync($"tenant:{_tenantResolver.AccountId}:identity:{request.IdentityName}:item", async entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

                var identityMembersOfRepo = _dbContext.Set<Domain.IdentityMemberOf>();
                var identityRepo = _dbContext.Set<Domain.Identity>();
                var identity = await identityRepo.AsNoTracking()
                    .Where(o => o.Name.ToLower().Equals(request.IdentityName.ToLower()))
                    .FirstOrDefaultAsync(cancellationToken: cancellationToken);

                if (identity is null)
                {
                    throw new NotFoundException(LocalizationService.GetString("IdentityNotFound", request.IdentityName));
                }


                string[] assignedNames = [];
                if (request.WithAssignedItems)
                {
                    if (identity.Type == IdentityType.Group)
                    {
                        assignedNames = await identityMembersOfRepo
                                            .AsNoTracking()
                                            .Include(o => o.Identity)
                                            .Where(o => o.GroupId == identity.Id)
                                            .Select(o => o.Identity!.Name)
                                            .ToArrayAsync(cancellationToken: cancellationToken);
                    }
                    else
                    {
                        assignedNames = await identityMembersOfRepo
                                            .AsNoTracking()
                                            .Include(o => o.Group)
                                            .Where(o => o.IdentityId == identity.Id)
                                            .Select(o => o.Group!.Name)
                                            .ToArrayAsync(cancellationToken: cancellationToken);
                    }
                }

                return new IdentityWithAssignedItem(identity.Id, identity.Name, identity.DisplayName, identity.Role, identity.Type, assignedNames, identity.ModifiedAt);
            }, cancellationToken);
        }
    }
}
