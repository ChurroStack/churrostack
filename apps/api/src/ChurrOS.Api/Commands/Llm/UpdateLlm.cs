using ChurrOS.Api.Models.Dtos.Llm;
using DispatchR.Abstractions.Send;
using System.Text.Json;

namespace ChurrOS.Api.Commands.Llm
{
    public class UpdateLlm : IRequest<UpdateLlm, ValueTask<LlmItem>>
    {
        public long LlmId { get; private set; }

        public JsonElement Body { get; private set; }

        public UpdateLlm(long llmId, JsonElement body)
        {
            LlmId = llmId;
            Body = body;
        }
    }
}
