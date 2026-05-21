using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Llm
{
    public class DeleteLlm : IRequest<DeleteLlm, Task>
    {
        public long LlmId { get; private set; }

        public DeleteLlm(long llmId)
        {
            LlmId = llmId;
        }
    }
}
