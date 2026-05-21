using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Identity
{
    public class GetIdentities : IRequest<GetIdentities, ValueTask<QueryResult<IdentityItem>>>
    {
        public long Top { get; set; } = 10;
        public long Skip { get; set; } = 0;
        public string Search { get; set; }
        public string[]? IncludeNames { get; set; }
        public string? Type { get; set; }
        public string? Role { get; set; }

        public GetIdentities(long top, long skip, string search, string[]? includeNames, string? type, string? role)
        {
            Top = top;
            Skip = skip;
            Search = search;
            IncludeNames = includeNames;
            Type = type;
            Role = role;
        }
    }
}
