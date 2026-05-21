using ChurrOS.Api.Models.Dtos.Account;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Account
{
    public class GetAccount : IRequest<GetAccount, ValueTask<AccountSummary>>
    {
    }
}
