using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.IdentitySync;
using ChurrOS.Api.Services.Entra;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.IdentitySync
{
    public class GetEntraSyncConfigHandler : IRequestHandler<GetEntraSyncConfig, ValueTask<EntraSyncConfigItem>>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly Services.ITenantResolver _tenantResolver;
        private readonly IMediator _mediator;
        private readonly IConfiguration _configuration;

        public GetEntraSyncConfigHandler(ChurrosDbContext dbContext, Services.ITenantResolver tenantResolver, IMediator mediator, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _tenantResolver = tenantResolver;
            _mediator = mediator;
            _configuration = configuration;
        }

        public async ValueTask<EntraSyncConfigItem> Handle(GetEntraSyncConfig request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _dbContext.IdentityId), cancellationToken);

            // Read straight from the DB: GetTenant() caches the Account in-process for 1h and would serve stale settings.
            var accountId = _tenantResolver.AccountId;
            var account = await _dbContext.Set<Domain.Account>().AsNoTracking().Where(o => o.Id == accountId).Select(o => new { o.Metadata }).SingleAsync(cancellationToken);

            var config = EntraSyncConfigStore.Read(account.Metadata);
            config.TenantId = EntraSyncConfigStore.ResolveTenantId(account.Metadata, _configuration); // read-only: operator-set
            return new EntraSyncConfigItem { Config = config, Available = EntraSyncConfigStore.IsAvailable(account.Metadata, _configuration) };
        }
    }
}
