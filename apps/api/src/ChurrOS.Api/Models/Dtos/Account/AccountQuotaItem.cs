namespace ChurrOS.Api.Models.Dtos.Account
{
    public class AccountQuotaItem
    {
        public long Network { get; protected set; }
        public long Environments { get; protected set; }
        public long Applications { get; protected set; }

        public AccountQuotaItem(long network, long environments, long applications)
        {
            Network = network;
            Environments = environments;
            Applications = applications;
        }
    }
}
