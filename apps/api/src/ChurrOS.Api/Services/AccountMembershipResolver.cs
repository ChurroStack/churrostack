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
                    // ExecuteQueryAsync is parameterized. ExecuteScalarAsync($"...") would bind to the raw-string overload
                    // (unquoted identity name => SQL error => caller gets 403), so it is not used here.
                    var active = await _dbContext.ExecuteQueryAsync<long>($"SELECT account_id FROM cs.identity WHERE account_id = {requestedAccountId.Value} AND name = {identityName} AND (lock_after IS NULL OR lock_after > now())");
                    if (active.Count == 1)
                        return requestedAccountId.Value;

                    var any = await _dbContext.ExecuteQueryAsync<long>($"SELECT account_id FROM cs.identity WHERE account_id = {requestedAccountId.Value} AND name = {identityName}");
                    if (any.Count == 1)
                    {
                        // Disabled in the requested account (e.g. removed from Entra): deny, do not silently serve another account.
                        _logger.LogWarning("[TenantResolution] identity={IdentityName} requestedAccountId={AccountId} outcome=disabled", identityName, requestedAccountId.Value);
                        return null;
                    }

                    _logger.LogWarning("[TenantResolution] identity={IdentityName} requestedAccountId={AccountId} outcome=membership_not_found", identityName, requestedAccountId.Value);
                }

                var accountIds = await _dbContext.ExecuteQueryAsync<long>($"SELECT account_id FROM cs.identity WHERE name = {identityName} AND (lock_after IS NULL OR lock_after > now())");
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
