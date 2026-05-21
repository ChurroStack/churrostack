using DispatchR.Abstractions.Send;
using System.Text.Json;

namespace ChurrOS.Api.Commands.Llm
{
    public class RerankWithLlm : IRequest<RerankWithLlm, ValueTask<JsonElement>>
    {
        public JsonElement Body { get; private set; }
        public string Api { get; private set; }
        public string? XUserId { get; private set; }

        public RerankWithLlm(JsonElement body, string api, string? xUserId)
        {
            Body = body;
            XUserId = xUserId;
            Api = api;
        }
    }
}
