using DispatchR.Abstractions.Send;
using System.Text.Json;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetEmbeddingsWithLlm : IRequest<GetEmbeddingsWithLlm, ValueTask<JsonElement>>
    {
        public JsonElement Body { get; private set; }
        public string? XUserId { get; private set; }

        public GetEmbeddingsWithLlm(JsonElement body, string? xUserId)
        {
            Body = body;
            XUserId = xUserId;
        }
    }
}
