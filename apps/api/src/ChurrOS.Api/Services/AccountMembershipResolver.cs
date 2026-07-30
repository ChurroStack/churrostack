using ChurrOS.Api.Data;

namespace ChurrOS.Api.Services
{
    public class AccountMembershipResolver : IAccountMembershipResolver
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly ILogger<AccountMembershipResolver> _logger;

        public AccountMembershipResolver(ChurrosDbContext dbContext, ILogger<AccountMembershipResolver> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<long?> ResolveAccountIdAsync(string identityName, long? requestedAccountId, CancellationToken cancellationToken)
        {
            try
            {
                if (requestedAccountId.HasValue)
                {
                    var count = await _dbContext.ExecuteScalarAsync<long?>($"SELECT COUNT(1) FROM cs.identity WHERE account_id = {requestedAccountId.Value} AND name = {identityName}");
                    if (count == 1)
                        return requestedAccountId.Value;

                    _logger.LogWarning("[TenantResolution] identity={IdentityName} requestedAccountId={AccountId} outcome=membership_not_found", identityName, requestedAccountId.Value);
                }

                var accountIds = await _dbContext.ExecuteQueryAsync<long>($"SELECT account_id FROM cs.identity WHERE name = {identityName}");
                return accountIds.Count == 1 ? accountIds[0] : null;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "[TenantResolution] identity={IdentityName} requestedAccountId={AccountId} outcome=resolution_failed", identityName, requestedAccountId);
                return null;
            }
        }
    }
}
