using ChurrOS.Api.Models.Dtos.Account;
using Mapster;

namespace ChurrOS.Api.Mappers
{
    public class AccountMapper : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            TypeAdapterConfig<Domain.Account, AccountSummary>
                .NewConfig()
                .Map(dest => dest.Quotas, src => new QuotaItem[0]);
        }
    }
}
