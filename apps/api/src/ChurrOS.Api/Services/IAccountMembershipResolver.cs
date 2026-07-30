namespace ChurrOS.Api.Services
{
    public interface IAccountMembershipResolver
    {
        Task<long?> ResolveAccountIdAsync(string identityName, long? requestedAccountId, CancellationToken cancellationToken);
    }
}
