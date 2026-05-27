using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Tags
{
    public class GetEnvironmentTags : IRequest<GetEnvironmentTags, ValueTask<string[]>>
    {
        public Permission Permission { get; private set; }

        public GetEnvironmentTags(Permission permission = Permission.Read)
        {
            Permission = permission;
        }
    }
}
