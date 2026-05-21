using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.ApiKeys
{
    public class DeleteApiKey : IRequest<DeleteApiKey, Task>
    {
        public long Id { get; }

        public DeleteApiKey(long id)
        {
            Id = id;
        }
    }
}
