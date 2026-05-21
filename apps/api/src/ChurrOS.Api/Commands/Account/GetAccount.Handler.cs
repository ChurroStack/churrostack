using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Account;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Account
{
    public class GetAccountHandler : IRequestHandler<GetAccount, ValueTask<AccountSummary>>
    {
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly IMapper _mapper;
        private readonly QuotaService _quotaService;
        private readonly IConfiguration _configuration;

        public GetAccountHandler(IMediator mediator, ChurrosDbContext context, ITenantResolver tenantResolver, IMapper mapper, QuotaService quotaService, IConfiguration configuration)
        {
            _mediator = mediator;
            _context = context;
            _tenantResolver = tenantResolver;
            _mapper = mapper;
            _quotaService = quotaService;
            _configuration = configuration;
        }

        public async ValueTask<AccountSummary> Handle(GetAccount request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);

            var account = await _context.Set<Domain.Account>()
                .Where(a => a.Id == _tenantResolver.AccountId)
                .FirstOrDefaultAsync(cancellationToken);

            if (account == null)
                throw new NotFoundException("Account not found.");

            var item = _mapper.Map<AccountSummary>(account!);

            var redisQuotas = await _quotaService.GetTenantQuotaAsync();

            var quotas = new List<QuotaItem>(redisQuotas.Select(o => new QuotaItem(o.Type.ToString().ToLowerInvariant(), o.used, o.total)));

            var quota = _context.Quota;

            quotas.Add(new QuotaItem("environments", await _context.Set<Domain.Environment>().Where(e => e.AccountId == account.Id).CountAsync(cancellationToken), quota.Environments));
            quotas.Add(new QuotaItem("applications", await _context.Set<Domain.Application>().Where(a => a.AccountId == account.Id).CountAsync(cancellationToken), quota.Applications));

            item.Quotas = quotas.ToArray();

            return item;
        }
    }
}
