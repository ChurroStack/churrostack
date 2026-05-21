using ChurrOS.Api.Models.Dtos.ApiKey;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.ApiKeys
{
    public class GetApiKey : IRequest<GetApiKey, ValueTask<ApiKeyItem>>
    {
        public long Id { get; }

        public GetApiKey(long id)
        {
            Id = id;
        }
    }
}
