using ChurrOS.Api.Models.Dtos.Llm;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetLlmById : IRequest<GetLlmById, ValueTask<LlmItem>>
    {
        public long LlmId { get; private set; }

        public GetLlmById(long llmId)
        {
            LlmId = llmId;
        }
    }
}
