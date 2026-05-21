namespace ChurrOS.Api.Models.Dtos.Account
{
    public class QuotaItem
    {
        public string Name { get; protected set; }
        public double Used { get; protected set; }
        public double Limit { get; protected set; }

        public QuotaItem(string name, double used, double limit)
        {
            Name = name;
            Used = used;
            Limit = limit;
        }
    }
}
