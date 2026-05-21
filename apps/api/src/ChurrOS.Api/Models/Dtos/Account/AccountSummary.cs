namespace ChurrOS.Api.Models.Dtos.Account
{
    public class AccountSummary
    {
        public string Name { get; protected set; }

        public string[] Owners { get; protected set; }

        public QuotaItem[] Quotas { get; set; }

        public AccountSummary(string name, string[] owners, QuotaItem[] quotas)
        {
            Name = name;
            Owners = owners;
            Quotas = quotas;
        }
    }
}
