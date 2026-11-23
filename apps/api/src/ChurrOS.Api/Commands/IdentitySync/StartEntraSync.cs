using ChurrOS.Api.Models.Dtos.IdentitySync;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.IdentitySync
{
    public class StartEntraSync : IRequest<StartEntraSync, ValueTask<EntraSyncStatus>>
    {
    }
}
