using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Tags
{
    public class GetApplicationTags : IRequest<GetApplicationTags, ValueTask<string[]>>
    {
        public Permission Permission { get; private set; }

        public GetApplicationTags(Permission permission = Permission.Read)
        {
            Permission = permission;
        }
    }
}
